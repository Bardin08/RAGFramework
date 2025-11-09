namespace RAG.Core.Domain.Enums;

/// <summary>
/// Defines the types of queries based on reasoning complexity required.
/// </summary>
public enum QueryType
{
    /// <summary>
    /// Direct factual questions with explicit answers.
    /// Example: "What is the capital of France?"
    /// </summary>
    ExplicitFact,

    /// <summary>
    /// Questions requiring simple inference from facts.
    /// Example: "What programming language is used in this .NET project?"
    /// </summary>
    ImplicitFact,

    /// <summary>
    /// Questions requiring explanation of reasoning.
    /// Example: "Why should I use Clean Architecture?"
    /// </summary>
    InterpretableRationale,

    /// <summary>
    /// Complex reasoning questions with non-obvious answers.
    /// Example: "How would combining RAG with multi-step reasoning improve accuracy?"
    /// </summary>
    HiddenRationale
}
