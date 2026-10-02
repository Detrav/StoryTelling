namespace StoryTelling.ViewModels;

public sealed class BookCompletionItemViewModel : ProgressItemViewModel
{
    public BookCompletionItemViewModel(BookOperation operation)
        : base(operation.Header, operation.Kind == BookOperationKind.Skip ? operation.SkipReason : null)
    {
        Operation = operation;
    }

    public BookOperation Operation { get; }
}
