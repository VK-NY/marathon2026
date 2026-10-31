using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui.Components;

/// <summary>One participant on the Owner's screen: "My Pre-Order" or "Guest: name".</summary>
public sealed class ParticipantCardComponent(ILocator root)
{
    public ILocator Root => root;
    public ILocator Status => root.Locator(".chip");
    public ILocator Items => root.Locator("li.item");
    public ILocator NoDishes => root.Locator("p.empty");
    public ILocator Subtotal => root.Locator("p.subtotal span").Last;
    public ILocator RemoveButton => root.Locator("button.remove");
}
