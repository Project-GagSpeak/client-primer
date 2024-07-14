namespace GagSpeak.UI.Components;

public enum OrdersTabSelection
{
    None, // shouldn't see this ever
    ActiveOrders, // Displays the currently active orders of the client.
    CreateOrder, // interface for the creation of an order for another user pair.
    // possibly other things here but look into it later.
}

/// <summary> Tab Menu for the GagSetup UI </summary>
public class OrdersTabMenu : TabMenuBase
{
    /// <summary> Defines the type of tab selection to use. </summary>
    protected override Type TabSelectionType => typeof(OrdersTabSelection);

    public OrdersTabMenu() { }
}
