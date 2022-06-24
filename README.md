# SimpleDI

A scoped dependency-injection framework based on C# using statements. For example:

```
using (Dependencies.Inject(new MyClass()) {
    Foo();
}

void Foo() {
    var myClass = Dependencies.Fetch<MyClass>();
}
```

Still a work-in-progress - handling subtypes in a flexible way can make for a more confusing API, and then efficient multithreading is a whole other story.

One tricky problem that I've attempted to tacke so far was handling exceptions thrown in `finally` statements.
