using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using GagSpeak.Services;
using GagSpeak.Services.Events;
using GagSpeak.Services.Mediator;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using GagSpeak.Services.Migrations;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using System.Linq;
using OtterGui.Classes;

namespace GagSpeak.UI;

internal class MigrationsUI : WindowMediatorSubscriberBase
{
    private readonly MigrateGagStorage _gagStorageMigrator;
    private readonly MigratePatterns _patternMigrator;
    private readonly MigrateRestraintSets _wardrobeMigrator;
    private readonly UiSharedService _uiShared;

    public MigrationsUI(ILogger<EventViewerUI> logger, GagspeakMediator mediator,
        MigrateGagStorage migrateGagStorage, MigratePatterns migratePatterns,
        MigrateRestraintSets migrateRestraintSets, UiSharedService uiShared) 
        : base(logger, mediator, "GagSpeak Migrations")
    {
        _gagStorageMigrator = migrateGagStorage;
        _patternMigrator = migratePatterns;
        _wardrobeMigrator = migrateRestraintSets;
        _uiShared = uiShared;

        SizeConstraints = new()
        {
            MinimumSize = new(500, 300),
            MaximumSize = new(1000, 2000)
        };
    }

    public string GagStorageSearchString = string.Empty;
    public string RestraintSetSearchString = string.Empty;
    public string PatternSearchString = string.Empty;


    protected override void PreDrawInternal() { }
    protected override void PostDrawInternal() { }
    protected override void DrawInternal()
    {
        _uiShared.BigText("GagSpeak Migrations");
        // draw our separator
        ImGui.Separator();
        // draw out the tab bar for us.
        if (ImGui.BeginTabBar("migrationsTabBar"))
        {
            if (ImGui.BeginTabItem("GagStorage Migrations"))
            {
                DrawGagstorageMigrations();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Restraint Set Migrations"))
            {
                DrawWardrobeMigrations();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Pattern Migrations"))
            {
                DrawPatternMigrations();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawGagstorageMigrations()
    {
        if (!_gagStorageMigrator.OldGagStorageLoaded)
        {
            ImGui.AlignTextToFramePadding();
            if (_uiShared.IconTextButton(FontAwesomeIcon.CloudDownloadAlt, "Load Old Gag Storage Data"))
            {
                _gagStorageMigrator.LoadOldGagStorage();
            }
        }
        else
        {
            _uiShared.BigText("Old Gag Storage Loaded Successfully");

            if (_uiShared.IconTextButton(FontAwesomeIcon.FileDownload, "Migrate GagStorage Data to Current Gag Storage", ImGui.GetContentRegionAvail().X))
            {
                _gagStorageMigrator.MigrateGagStorageToCurrentGagStorage();
            }
        }
    }

    private void DrawWardrobeMigrations()
    {
        if (!_wardrobeMigrator.OldRestraintSetsLoaded)
        {
            ImGui.AlignTextToFramePadding();
            if (_uiShared.IconTextButton(FontAwesomeIcon.CloudDownloadAlt, "Load Old Wardrobe Restraint Sets"))
            {
                _wardrobeMigrator.LoadOldRestraintSets();
            }
        }
        else
        {
            _uiShared.BigText("Migrate Sets (Total: " + _wardrobeMigrator.OldRestraintSets.RestraintSets.Count + ")");
            _uiShared.DrawComboSearchable("Select Set from Storage", 250f, ref RestraintSetSearchString,
                _wardrobeMigrator.OldRestraintSets.RestraintSets.Select(p => p.Name).ToList(), (i) => i, true,
            (i) =>
            {
                // locate the index of the pattern
                _wardrobeMigrator.SelectedRestraintSetIdx = _wardrobeMigrator.OldRestraintSets.RestraintSets.FindIndex(p => p.Name == i);
            });

            ImGui.Separator();

            // draw the pattern info:
            _uiShared.BigText(_wardrobeMigrator.TmpOldRestraintSet.Name);
            // display the information about the imported restraint set.
            ImGui.AlignTextToFramePadding();
            ImGui.Text($"Description: {_wardrobeMigrator.TmpOldRestraintSet.Description}");

            ImGui.AlignTextToFramePadding();
            ImGui.Text($"Enabled:");
            ImGui.SameLine();
            _uiShared.BooleanToColoredIcon(_wardrobeMigrator.TmpOldRestraintSet.Enabled);

            ImGui.AlignTextToFramePadding();
            ImGui.Text($"Locked:");
            ImGui.SameLine();
            _uiShared.BooleanToColoredIcon(_wardrobeMigrator.TmpOldRestraintSet.Locked);

            ImGui.AlignTextToFramePadding();
            ImGui.Text($"DrawData:");

            if(ImGui.CollapsingHeader("Draw Data"))
            {
                foreach (var (slot, data) in _wardrobeMigrator.TmpOldRestraintSet.DrawData)
                {
                    ImGui.Text($"Slot: {slot}");
                    ImGui.Indent();
                    ImGui.Text($"GameItem: {data.GameItem.Name}");
                    ImGui.Text($"GameItem ID: {data.GameItem.Id}");
                    ImGui.Text($"Stain: {data.GameStain.Id}");
                    ImGui.Unindent();
                }
            }

            ImGui.Separator();
            // draw the apply buttons
            var region = ImGui.GetContentRegionAvail().X;
            if (_uiShared.IconTextButton(FontAwesomeIcon.FileDownload, "Migrate Restraint Set", (region - ImGui.GetStyle().ItemSpacing.X) / 2))
            {
                _wardrobeMigrator.AppendOldRestraintSetToStorage(_wardrobeMigrator.SelectedRestraintSetIdx);
            }
            ImGui.SameLine();
            if (_uiShared.IconTextButton(FontAwesomeIcon.FileDownload, "Migrate ALL Restraint Sets", (region - ImGui.GetStyle().ItemSpacing.X) / 2))
            {
                _wardrobeMigrator.AppendAllOldRestraintSetToStorage();
            }
        }
    }

    private void DrawPatternMigrations()
    {
        if (!_patternMigrator.OldPatternsLoaded)
        {
            ImGui.AlignTextToFramePadding();
            if (_uiShared.IconTextButton(FontAwesomeIcon.CloudDownloadAlt, "Load Old Gagspeak Pattern Data"))
            {
                _patternMigrator.LoadOldPatterns();
            }
        }
        else
        {
            _uiShared.BigText("Migrate Patterns (Total: " + _patternMigrator.OldPatternStorage.PatternList.Count + ")");
            _uiShared.DrawComboSearchable("Select Pattern from Old Storage", 275f, ref PatternSearchString,
                _patternMigrator.OldPatternStorage.PatternList.Select(p => p.Name).ToList(), (i) => i, true,
            (i) =>
            {
                // locate the index of the pattern
                _patternMigrator.SelectedPatternIdx = _patternMigrator.OldPatternStorage.PatternList.FindIndex(p => p.Name == i);
            });

            ImGui.Separator();
            
            // draw the pattern info:
            _uiShared.BigText(_patternMigrator.TmpOldPatternData.Name);

            ImGui.AlignTextToFramePadding();
            ImGui.Text($"Description: {_patternMigrator.TmpOldPatternData.Description}");

            ImGui.AlignTextToFramePadding();
            ImGui.Text($"Duration: {_patternMigrator.TmpOldPatternData.Duration}");

            ImGui.AlignTextToFramePadding();
            ImGui.Text($"IsActive:");
            ImGui.SameLine();
            _uiShared.BooleanToColoredIcon(_patternMigrator.TmpOldPatternData.IsActive);

            ImGui.AlignTextToFramePadding();
            ImGui.Text($"Loop:");
            ImGui.SameLine();
            _uiShared.BooleanToColoredIcon(_patternMigrator.TmpOldPatternData.Loop);

            ImGui.Separator();
            // draw the apply buttons
            var region = ImGui.GetContentRegionAvail().X;
            if (_uiShared.IconTextButton(FontAwesomeIcon.FileDownload, "Migrate Pattern", (region - ImGui.GetStyle().ItemSpacing.X) / 2))
            {
                _patternMigrator.AppendOldPatternToPatternStorage(_patternMigrator.SelectedPatternIdx);
            }
            ImGui.SameLine();
            if (_uiShared.IconTextButton(FontAwesomeIcon.FileDownload, "Migrate ALL Patterns", (region - ImGui.GetStyle().ItemSpacing.X) / 2))
            {
                _patternMigrator.AppendAllOldPatternsToPatternStorage();
            }
        }
    }
}
