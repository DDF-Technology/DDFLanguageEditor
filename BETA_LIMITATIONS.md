# Beta 0.1 limitations

This release is intended for interface and language-design evaluation only.

## Implemented

- local text editing;
- visible line numbers;
- experimental token and block highlighting;
- four-space Tab and assisted newline indentation.

## Not implemented

- file open, save, recent-file or recovery workflows;
- lexical or syntactic validation;
- parser, compiler, interpreter or runtime;
- build, run or debug commands;
- project/workspace management;
- extension or plug-in APIs;
- stability guarantees for the DDF syntax.

The editor content is not persisted. Close the application only after copying
any text that must be retained.
