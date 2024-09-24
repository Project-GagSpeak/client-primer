using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GagSpeak.Utils;
using GagspeakAPI.Enums;
using Lumina.Data.Parsing;
using static Lumina.Data.Parsing.Uld.NodeData;

namespace GagSpeak.UpdateMonitoring.Chat;

public static unsafe class ChatLogAddonHelper
{
    // Pulled from older version of Dalamud prior to using GetSingleton().
    // https://github.com/ottercorp/Dalamud/blob/f89e9ebca547a7a14fbd6acb64ee7d8cf666f1b3/Dalamud/Game/Gui/GameGui.cs#L283C19-L283C33
    public static unsafe IntPtr GetAddonByName(string name)
    {
        var atkStage = AtkStage.Instance();
        if (atkStage == null)
            return IntPtr.Zero;

        var unitMgr = atkStage->RaptureAtkUnitManager;
        if (unitMgr == null)
            return IntPtr.Zero;

        var addon = unitMgr->GetAddonByName(name, 1);
        if (addon == null)
            return IntPtr.Zero;

        return (IntPtr)addon;
    }

    private static AddonChatLogPanel* ChatLogMain => (AddonChatLogPanel*)(AtkUnitBase*)GetAddonByName("ChatLog");
    private static AddonChatLogPanel*[] ChatLogPanels = new AddonChatLogPanel*[]
    {
        (AddonChatLogPanel*)(AtkUnitBase*)GetAddonByName("ChatLogPanel_0"),
        (AddonChatLogPanel*)(AtkUnitBase*)GetAddonByName("ChatLogPanel_1"),
        (AddonChatLogPanel*)(AtkUnitBase*)GetAddonByName("ChatLogPanel_2"),
        (AddonChatLogPanel*)(AtkUnitBase*)GetAddonByName("ChatLogPanel_3")
    };

    // https://github.com/Caraxi/SimpleTweaksPlugin/blob/0cf2c68a2e6411d667af0851ca36f0ff59d21626/Tweaks/Chat/HideChatAuto.cs#L26
    private const uint TextInputNodeID = 5;
    private const uint TextInputCursorID = 2;
    private static AtkResNode* GetChatInputCursorNode()
    {
        var baseNode = (AtkUnitBase*)GetAddonByName("ChatLog");
        if (baseNode == null) return null;

        var textInputComponentNode = (AtkComponentNode*)baseNode->GetNodeById(TextInputNodeID);
        if (textInputComponentNode == null || textInputComponentNode->Component == null) return null;

        var uldManager = textInputComponentNode->Component->UldManager;

        if (uldManager.NodeList == null) return null;

        // Inline search for the node by ID
        for (var i = 0; i < uldManager.NodeListCount; i++)
        {
            var n = uldManager.NodeList[i];
            if (n != null && n->NodeId == TextInputCursorID) // Add NodeType check if necessary
                return n;
        }

        return null;
    }

    public static bool IsChatFocused()
    {
        var inputCursorNode = GetChatInputCursorNode();
        if (inputCursorNode == null) return false;

        return inputCursorNode->IsVisible();
    }

    public static unsafe void DiscardCursorNodeWhenFocused()
    {
        var inputCursorNode = GetChatInputCursorNode();
        if (inputCursorNode == null) return;

        if(inputCursorNode->IsVisible())
            RaptureAtkModule.Instance()->ClearFocus();
    }

    public static unsafe bool IsChatInputVisible => ChatLogMain->AtkUnitBase.RootNode->IsVisible();
    public static unsafe bool IsChatPanelVisible(int panelIndex) => ChatLogPanels[panelIndex]->RootNode->IsVisible();

    public static unsafe void SetMainChatLogVisibility(bool state)
    {
        if (ChatLogMain->AtkUnitBase.RootNode != null)
            ChatLogMain->AtkUnitBase.RootNode->ToggleVisibility(state);
    }

    public static unsafe void SetChatLogPanelsVisibility(bool state)
    {
        foreach (var panel in ChatLogPanels)
            if (panel->RootNode != null)
                panel->RootNode->ToggleVisibility(state);
    }


    public static unsafe void SetAllChatLogState(bool newState)
    {
        SetMainChatLogVisibility(newState);
        SetChatLogPanelsVisibility(newState);
    }
}
