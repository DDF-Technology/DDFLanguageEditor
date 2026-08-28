namespace DDFLanguageEditor.Core
{
    public enum DdfTokenKind
    {
        BadToken,
        Identifier,
        NumberLiteral,
        BooleanLiteral,
        StringLiteral,
        LibraryDirective,
        LineComment,
        BlockComment,
        DataTypeKeyword,
        FunctionKeyword,
        ControlFlowKeyword,
        Operator,
        Punctuation
    }
}
