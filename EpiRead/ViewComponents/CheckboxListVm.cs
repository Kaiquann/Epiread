namespace EpiRead.ViewComponents;

/// <summary>
///     Generic model for rendering a checkbox list for any item type.
/// </summary>
public class CheckboxListVm<TItem, TKey>
{
    /// <summary>
    ///     The list of items to display as checkboxes.
    /// </summary>
    public IEnumerable<TItem> Items { get; set; } = new List<TItem>();

    /// <summary>
    ///     The values that should be checked.
    /// </summary>
    public IEnumerable<TKey> SelectedValues { get; set; } = new List<TKey>();

    /// <summary>
    ///     The name attribute for the checkbox inputs.
    /// </summary>
    public string Name { get; set; } = "CheckboxList";

    /// <summary>
    ///     The property name to use for the value of each checkbox.
    /// </summary>
    public string ValueProperty { get; set; } = "Id";

    /// <summary>
    ///     The property name to use for the display text of each checkbox.
    /// </summary>
    public string TextProperty { get; set; } = "Name";
}