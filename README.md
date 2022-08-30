# SimpleDI

A scoped dependency-injection/service-locator framework based on C# `using` statements. For example:

```C#
using (Dependencies.Inject<IMyClass>(new MyClass()) {
    Foo();
}

void Foo() {
    var myClass = Dependencies.Fetch<IMyClass>();
}
```

Still a work-in-progress - handling subtypes in a flexible way can make for a more confusing API, and then efficient multithreading is a whole other story.

One tricky problem that I've attempted to tackle so far was handling exceptions thrown in `finally` statements.
