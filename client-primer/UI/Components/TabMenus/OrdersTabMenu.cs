namespace GagSpeak.UI.Components;

/// <summary> Tab Menu for the GagSetup UI </summary>
public class OrdersTabMenu : TabMenuBase
{
    /// <summary> Defines the type of tab selection to use. </summary>
    protected override Type TabSelectionType => typeof(OrdersTabs.Tabs);

    public OrdersTabMenu() { }

    protected override string GetTabDisplayName(Enum tab)
    {
        if (tab is OrdersTabs.Tabs ordersTabs)
        {
            return OrdersTabs.GetTabName(ordersTabs);
        }

        return "Unknown"; // Fallback for tabs that don't match the expected type.
    }
}

public static class OrdersTabs
{
    public enum Tabs
    {
        ActiveOrders, // Displays the currently active orders of the client.
        CreateOrder, // creates order presets that can be assigned to others
        AssignOrder, // interface for the creation of an order for another user pair.
    }

    public static string GetTabName(Tabs tab)
    {
        return tab switch
        {
            Tabs.ActiveOrders => "Active Orders",
            Tabs.CreateOrder => "Create Order",
            Tabs.AssignOrder => "Assign Order",
            _ => "None",
        };
    }
}
