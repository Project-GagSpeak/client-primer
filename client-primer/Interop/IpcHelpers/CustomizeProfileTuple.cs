namespace GagSpeak.Interop.IpcHelpers;
public struct CustomizePlusProfileData
{
    public Guid UniqueId { get; set; }
    public string Name { get; set; }
    public string VirtualPath { get; set; }
    public string CharacterName { get; set; }
    public bool IsEnabled { get; set; }

    public CustomizePlusProfileData(Guid uniqueId, string name, string virtualPath, 
        string characterName, bool isEnabled)
    {
        UniqueId = uniqueId;
        Name = name;
        VirtualPath = virtualPath;
        CharacterName = characterName;
        IsEnabled = isEnabled;
    }
}
