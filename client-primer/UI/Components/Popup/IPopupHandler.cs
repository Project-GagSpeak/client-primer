using System.Numerics;

namespace GagSpeak.UI.Components.Popup;

/// <summary> A interface for handling the popups in the UI. </summary>
public interface IPopupHandler
{
    Vector2 PopupSize { get; }
    bool ShowClose { get; }

    void DrawContent();
}
