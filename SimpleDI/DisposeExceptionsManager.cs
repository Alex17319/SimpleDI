using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleDI
{
	public static class DisposeExceptionsManager
	{
		private static int currentRegionLevel = SafeDisposeExceptionsRegion.RegionLevel_NoneOpen;

		private static long currentRegionID = SafeDisposeExceptionsRegion.MinValidRegionID;
		private static long nextNewRegionID = currentRegionID + 1;



		public static event SDERDisposeErrorEvent RegionCloseError;



		private static Exception _lastException = null;

		public static bool InSafeDisposeExceptionsEnvironment
			=> currentRegionLevel >= SafeDisposeExceptionsRegion.RegionLevel_OneOpen;

		// Not all that safe & no clear use case
		//	/// <summary>
		//	/// TODO: WARNINGS
		//	/// </summary>
		//	public static Exception LastExceptionThrown
		//		=> InSafeDisposeExceptionsEnvironment ? _lastException : throw new InvalidOperationException(
		//			$"Not currently in safe dispose exceptions environment. Please check {InSafeDisposeExceptionsEnvironment} first."
		//		);


		/// <summary>
		/// TODO: WARNINGS, SHOULD BE CALLED FROM FINALLY/DISPOSE ONLY
		/// </summary>
		/// <returns></returns>
		public static DisposeInnerException WrapLastExceptionThrown()
			=> !InSafeDisposeExceptionsEnvironment ? null : new DisposeInnerException(
				"As an exception has been thrown from a finally/Dispose() region, it would normally overwrite any " +
				"exception already propagating up the stack. Instead, the previous exception thrown has been captured " +
				"here. However, if no exception was propagating, the exception shown here would be an exception " +
				"already caught & handled earlier, and so the one here may have no relevance to the current exception.",
				_lastException
			);


		private static void beginSafeDisposeExEnvironment()
		{
			AppDomain.CurrentDomain.FirstChanceException += FirstChanceExceptionHandler;
		}

		private static void endSafeDisposeExEnvironment()
		{
			AppDomain.CurrentDomain.FirstChanceException -= FirstChanceExceptionHandler;
			_lastException = null;
		}

		private static void FirstChanceExceptionHandler(object sender, FirstChanceExceptionEventArgs e)
		{
			// TODO: See if this works (allows the exception to either keep its full stack trace, or
			// have its stack trace built up as it propagates and allow us to view these changes) or
			// whether we need to look into using ExceptionDispatchInfo.
			// https://stackoverflow.com/a/12619055/4149474

			_lastException = e.Exception;
		}



		/// <summary>
		/// <see langword="[Call inside using()]"></see>
		/// 
		/// </summary>
		/// <returns></returns>
		public static SafeDisposeExceptionsRegion SafeDisposeExceptions()
		{
			if (currentRegionLevel == int.MaxValue) throw new InvalidOperationException(
				$"Maximum region level ({int.MaxValue}) has been reached."
			);
			if (nextNewRegionID == long.MaxValue) throw new InvalidOperationException(
				$"Maximum region ID ({long.MaxValue}) has been reached."
			);
			// ^ The alternative to checking these explicitly is to use 'checked', and then use two nested
			// try catch blocks to undo the beginSafeDisposeExEnvironment() and currentRegionLevel++
			// lines if something goes wrong (before rethrowing the exception). This way seems much cleaner.

			beginSafeDisposeExEnvironment();

			currentRegionLevel++;

			var result = new SafeDisposeExceptionsRegion(currentRegionLevel, nextNewRegionID, currentRegionID);

			currentRegionID = nextNewRegionID;
			nextNewRegionID++;

			return result;
		}

		internal static void CloseSafeDisposeExceptionsRegion(SafeDisposeExceptionsRegion region)
		{
			// This method must not throw any exceptions, but must be able to handle:
			//  - Regions being disposed of multiple times (they're structs so could be copied, and then both
			//    copies disposed. Classes wouldn't really help though as reflection's still possible)
			//  - An inner region not being disposed, and then the outer region being disposed.
			//  - Regions being disposed of in the wrong order i.e. not the reverse of the order they were created
			// 
			// Fortunately, we only actually need to do any disposing work when the outermost region is entered
			// and disposed - all the inner regions are just to allow code in a safe region to call more code
			// that internally uses another safe region, and don't need any cleanup.
			// 
			// However, so that we don't completely fail silently, use events to flag any unexpected situations
			// to make them more possible to debug. If no handler is present, then by defualt print a message to
			// the console and debug logger.
			//
			// It's ok to allow handlers to throw exceptions - yes they'll hide any existing exceptions but that's
			// their own responsibility to deal with, nothing we can really do here. Eg. if it's something like an
			// OutOfMemoryException we can't really swallow it and print it to the console.
			// 
			// Overall it's ok to just use events & printing for errors here, rather than making everything stop,
			// as the worst that's likely to happen is the safe dispose exceptions envonriment keeps running for
			// longer. On the other hand, it's *not* ok to do the same with the Dependencies stack, as if the
			// message doesn't get printed (maybe there's both no console and no debugger log) then all the
			// dependencies used by various code could be silently screwed up.

			// Do all checking for errors, invoking of handler methods, and printing of errors.
			// After this, we just have to worry about whether any cleanup is needed,
			// and how to restore things to a state that makes the most sense.
			if (region.ID != currentRegionID)
			{
				if (RegionCloseError == null)
					printErrorMessage(getRegionCloseIDMismatchMsg(region, currentRegionID, currentRegionLevel));
				else
					RegionCloseError(null, new SDERDisposeErrorEventArgs(region, currentRegionLevel, currentRegionID));
			}


			if (currentRegionLevel == SafeDisposeExceptionsRegion.RegionLevel_NoneOpen) return; // Nothing to clean up

			if (region.level > currentRegionLevel) return; // Already closed, no cleanup needed

			if (region.level < currentRegionLevel)
			{
				// Inner regions didn't get closed, just jump out to the current
				// region-to-exit (closing all the inner ones in the process)
				currentRegionLevel = region.level;
				currentRegionID = region.ID;
			}

			// Now close the current region
			currentRegionID = region.parentRegionID;
			endSafeDisposeExEnvironment();
		}


		public static string getRegionCloseIDMismatchMsg(SafeDisposeExceptionsRegion region, long curRegionID, int curRegionLevel)
		{
			return (
				$"Warning: Unexpected situation when exiting {nameof(SafeDisposeExceptionsRegion)}: " +
				System.Environment.NewLine +
				$"Attempted to exit region with ID '{region.ID}' (at level '{region.level}')" +
				$"but currently open region has ID '{curRegionID} (and is at level {curRegionLevel})." +
				System.Environment.NewLine +
				$"If level-of-region-to-exit > current-level, then an outer region, or the current region to exit, " +
				$"may have already been closed. No changes will be made to the current state." +
				System.Environment.NewLine +
				$"If level-of-region-to-exit < current-level, then some inner regions may not have been closed properly." +
				$"These regions will be closed now, along with the region-to-exit, and if the region-to-exit is the " +
				$"outermost region (level {SafeDisposeExceptionsRegion.RegionLevel_NoneOpen}), the safe dispose " +
				$"exceptions environment will be cleaned up." +
				System.Environment.NewLine +
				$"If level-of-region-to-exit == current-level, then some other error presumably occurred previously." +
				$"The region-to-exit will be closed now, and if it is the outermost region " +
				$"(level {SafeDisposeExceptionsRegion.RegionLevel_NoneOpen}), the safe dispose exceptions environment " +
				$"will be cleaned up." +
				System.Environment.NewLine +
				$"Overall, IDs are used purely to detect this situation, while only the level is used to determine what " +
				$"region(s) are closed and whether any cleanup is performed. " +
				System.Environment.NewLine +
				$"region-to-exit: {region}" +
				System.Environment.NewLine +
				$"To run some code when this warning occurs, subscribe to " +
				$"{nameof(SimpleDI)}.{nameof(DisposeExceptionsManager)}.{nameof(RegionCloseError)}. " +
				$"Subscribing to that event will suppress this message (use an empty handler to just suppress it). " +
				$"{nameof(SimpleDI)}.{nameof(DisposeExceptionsManager)}.{nameof(getRegionCloseIDMismatchMsg)} " +
				$"may be used to generate this message again after any handling of the warning, " +
				$"and {nameof(SimpleDI)}.{nameof(DisposeExceptionsManager)}.{nameof(printErrorMessage)} " +
				$"may be used to print it."
			);
		}



		/// <summary>
		/// Used to print error messages when no error event handler is available,
		/// or by an error event handler after running some other code.
		/// </summary>
		/// <remarks>
		/// Writes a message using <see cref="Console.WriteLine(string)"/> and <see cref="Debug.WriteLine(string)"/>.
		/// </remarks>
		public static void printErrorMessage(string message)
		{
			Console.WriteLine(message);
			Debug.WriteLine(message);
		}



		public delegate void SDERDisposeErrorEvent(object sender, SDERDisposeErrorEventArgs e);

		public class SDERDisposeErrorEventArgs : EventArgs
		{
			public SafeDisposeExceptionsRegion Region { get; }
			public int CurrentRegionLevel { get; }
			public long CurrentRegionID { get; }

			public SDERDisposeErrorEventArgs(SafeDisposeExceptionsRegion region, int currentRegionLevel, long currentRegionID)
			{
				this.Region = region;
				this.CurrentRegionLevel = currentRegionLevel;
				this.CurrentRegionID = currentRegionID;
			}
		}
	}
}

//*/