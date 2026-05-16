using QDXM.Avalon.Core.Search;

namespace QDXM.Avalon.ViewModels;

public sealed record SearchArrangeOptionView(SearchArrangeOption Value, string Label)
{
    public override string ToString() => Label;
}
