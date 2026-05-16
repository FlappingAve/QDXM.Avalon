using Avalonia.Controls;
using Avalonia.Interactivity;
using QDXM.Avalon.ViewModels;

namespace QDXM.Avalon.Views;

public partial class TagsView : UserControl
{
    private TextBox? lastFocusedTemplateBox;

    public TagsView()
    {
        InitializeComponent();
    }

    private void TemplateTextBox_OnGotFocus(object? sender, RoutedEventArgs e)
    {
        lastFocusedTemplateBox = sender as TextBox;

    }

    private void InsertTemplateField_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TagsViewModel viewModel ||
            sender is not Button button ||
            button.Tag is not string tag)
        {
            return;
        }

        var parts = tag.Split('|', 2);
        if (parts.Length != 2)
        {
            return;
        }

        var targetBox = parts[0] switch
        {
            "Folder" => FolderTemplateBox,
            "Filename" => FilenameTemplateBox,
            "Disc" => DiscFolderTemplateBox,
            "PlaylistFolder" => PlaylistFolderTemplateBox,
            "PlaylistFilename" => PlaylistFilenameTemplateBox,
            _ => null
        };

        if (targetBox is null)
        {
            return;
        }

        var caretIndex = ReferenceEquals(lastFocusedTemplateBox, targetBox)
            ? targetBox.CaretIndex
            : targetBox.Text?.Length ?? 0;
        var newCaretIndex = viewModel.InsertTemplateField(parts[0], parts[1], caretIndex);

        targetBox.Focus();
        targetBox.CaretIndex = newCaretIndex;
        lastFocusedTemplateBox = targetBox;
    }
}
