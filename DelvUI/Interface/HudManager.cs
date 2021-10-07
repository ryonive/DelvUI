using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using DelvUI.Config;
using DelvUI.Helpers;
using DelvUI.Interface.GeneralElements;
using DelvUI.Interface.Jobs;
using DelvUI.Interface.Party;
using DelvUI.Interface.StatusEffects;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace DelvUI.Interface
{
    public class HudManager : IDisposable
    {
        private Vector2 _origin = ImGui.GetMainViewport().Size / 2f;

        private GridConfig? _gridConfig;
        private DraggableHudElement? _selectedElement = null;

        private List<DraggableHudElement> _hudElements = null!;
        private List<IHudElementWithActor> _hudElementsUsingPlayer = null!;
        private List<IHudElementWithActor> _hudElementsUsingTarget = null!;
        private List<IHudElementWithActor> _hudElementsUsingTargetOfTarget = null!;
        private List<IHudElementWithActor> _hudElementsUsingFocusTarget = null!;

        private UnitFrameHud _playerUnitFrameHud = null!;
        private UnitFrameHud _targetUnitFrameHud = null!;
        private UnitFrameHud _totUnitFrameHud = null!;
        private UnitFrameHud _focusTargetUnitFrameHud = null!;

        private PlayerCastbarHud _playerCastbarHud = null!;
        private CustomEffectsListHud _customEffectsHud = null!;
        private PrimaryResourceHud _playerManaBarHud = null!;
        private JobHud? _jobHud = null;
        private PartyFramesHud _partyFramesHud = null!;

        private Dictionary<uint, JobHudTypes> _jobsMap = null!;
        private Dictionary<uint, Type> _unsupportedJobsMap = null!;

        private double _occupiedInQuestStartTime = -1;

        private HudHelper _hudHelper = new HudHelper();

        public HudManager()
        {
            CreateJobsMap();

            ConfigurationManager.Instance.ResetEvent += OnConfigReset;
            ConfigurationManager.Instance.LockEvent += OnHUDLockChanged;

            CreateHudElements();
        }

        ~HudManager()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            _hudHelper.Dispose();

            _hudElements.Clear();
            _hudElementsUsingPlayer.Clear();
            _hudElementsUsingTarget.Clear();
            _hudElementsUsingTargetOfTarget.Clear();
            _hudElementsUsingFocusTarget.Clear();

            ConfigurationManager.Instance.ResetEvent -= OnConfigReset;
            ConfigurationManager.Instance.LockEvent -= OnHUDLockChanged;
        }

        private void OnConfigReset(ConfigurationManager sender)
        {
            CreateHudElements();
            _jobHud = null;
        }

        private void OnHUDLockChanged(ConfigurationManager sender)
        {
            var draggingEnabled = !sender.LockHUD;

            foreach (var element in _hudElements)
            {
                element.DraggingEnabled = draggingEnabled;
                element.Selected = false;
            }

            if (_jobHud != null)
            {
                _jobHud.DraggingEnabled = draggingEnabled;
            }

            _selectedElement = null;
        }

        private void OnDraggableElementSelected(DraggableHudElement sender)
        {
            foreach (var element in _hudElements)
            {
                element.Selected = element == sender;
            }

            if (_jobHud != null)
            {
                _jobHud.Selected = _jobHud == sender;
            }

            _selectedElement = sender;
        }

        private void CreateHudElements()
        {
            _gridConfig = ConfigurationManager.Instance.GetConfigObject<GridConfig>();

            _hudElements = new List<DraggableHudElement>();
            _hudElementsUsingPlayer = new List<IHudElementWithActor>();
            _hudElementsUsingTarget = new List<IHudElementWithActor>();
            _hudElementsUsingTargetOfTarget = new List<IHudElementWithActor>();
            _hudElementsUsingFocusTarget = new List<IHudElementWithActor>();

            CreateUnitFrames();
            CreateManaBars();
            CreateCastbars();
            CreateStatusEffectsLists();
            CreateMiscElements();

            foreach (var element in _hudElements)
            {
                element.SelectEvent += OnDraggableElementSelected;
            }
        }

        private void CreateUnitFrames()
        {
            var playerUnitFrameConfig = ConfigurationManager.Instance.GetConfigObject<PlayerUnitFrameConfig>();
            _playerUnitFrameHud = new UnitFrameHud("DelvUI_playerUnitFrame", playerUnitFrameConfig, "Player");
            _hudElements.Add(_playerUnitFrameHud);
            _hudElementsUsingPlayer.Add(_playerUnitFrameHud);

            var targetUnitFrameConfig = ConfigurationManager.Instance.GetConfigObject<TargetUnitFrameConfig>();
            _targetUnitFrameHud = new UnitFrameHud("DelvUI_targetUnitFrame", targetUnitFrameConfig, "Target");
            _hudElements.Add(_targetUnitFrameHud);
            _hudElementsUsingTarget.Add(_targetUnitFrameHud);

            var targetOfTargetUnitFrameConfig = ConfigurationManager.Instance.GetConfigObject<TargetOfTargetUnitFrameConfig>();
            _totUnitFrameHud = new UnitFrameHud("DelvUI_targetOfTargetUnitFrame", targetOfTargetUnitFrameConfig, "Target of Target");
            _hudElements.Add(_totUnitFrameHud);
            _hudElementsUsingTargetOfTarget.Add(_totUnitFrameHud);

            var focusTargetUnitFrameConfig = ConfigurationManager.Instance.GetConfigObject<FocusTargetUnitFrameConfig>();
            _focusTargetUnitFrameHud = new UnitFrameHud("DelvUI_focusTargetUnitFrame", focusTargetUnitFrameConfig, "Focus Target");
            _hudElements.Add(_focusTargetUnitFrameHud);
            _hudElementsUsingFocusTarget.Add(_focusTargetUnitFrameHud);

            var partyFramesConfig = ConfigurationManager.Instance.GetConfigObject<PartyFramesConfig>();
            _partyFramesHud = new PartyFramesHud("DelvUI_partyFrames", partyFramesConfig, "Party Frames");
            _hudElements.Add(_partyFramesHud);
        }

        private void CreateManaBars()
        {
            var playerManaBarConfig = ConfigurationManager.Instance.GetConfigObject<PlayerPrimaryResourceConfig>();
            _playerManaBarHud = new PrimaryResourceHud("DelvUI_playerManaBar", playerManaBarConfig, "Player Mana Bar");
            _playerManaBarHud.ParentConfig = _playerUnitFrameHud.Config;
            _hudElements.Add(_playerManaBarHud);
            _hudElementsUsingPlayer.Add(_playerManaBarHud);

            var targetManaBarConfig = ConfigurationManager.Instance.GetConfigObject<TargetPrimaryResourceConfig>();
            var targetManaBarHud = new PrimaryResourceHud("DelvUI_targetManaBar", targetManaBarConfig, "Target Mana Bar");
            targetManaBarHud.ParentConfig = _targetUnitFrameHud.Config;
            _hudElements.Add(targetManaBarHud);
            _hudElementsUsingTarget.Add(targetManaBarHud);

            var totManaBarConfig = ConfigurationManager.Instance.GetConfigObject<TargetOfTargetPrimaryResourceConfig>();
            var totManaBarHud = new PrimaryResourceHud("DelvUI_totManaBar", totManaBarConfig, "ToT Mana Bar");
            totManaBarHud.ParentConfig = _totUnitFrameHud.Config;
            _hudElements.Add(totManaBarHud);
            _hudElementsUsingTargetOfTarget.Add(totManaBarHud);

            var focusManaBarConfig = ConfigurationManager.Instance.GetConfigObject<FocusTargetPrimaryResourceConfig>();
            var focusManaBarHud = new PrimaryResourceHud("DelvUI_focusManaBar", focusManaBarConfig, "Focus Mana Bar");
            focusManaBarHud.ParentConfig = _focusTargetUnitFrameHud.Config;
            _hudElements.Add(focusManaBarHud);
            _hudElementsUsingFocusTarget.Add(focusManaBarHud);
        }

        private void CreateCastbars()
        {
            var playerCastbarConfig = ConfigurationManager.Instance.GetConfigObject<PlayerCastbarConfig>();
            _playerCastbarHud = new PlayerCastbarHud("DelvUI_playerCastbar", playerCastbarConfig, "Player Castbar");
            _playerCastbarHud.ParentConfig = _playerUnitFrameHud.Config;
            _hudElements.Add(_playerCastbarHud);
            _hudElementsUsingPlayer.Add(_playerCastbarHud);

            var targetCastbarConfig = ConfigurationManager.Instance.GetConfigObject<TargetCastbarConfig>();
            var targetCastbar = new TargetCastbarHud("DelvUI_targetCastbar", targetCastbarConfig, "Target Castbar");
            targetCastbar.ParentConfig = _targetUnitFrameHud.Config;
            _hudElements.Add(targetCastbar);
            _hudElementsUsingTarget.Add(targetCastbar);

            var targetOfTargetCastbarConfig = ConfigurationManager.Instance.GetConfigObject<TargetOfTargetCastbarConfig>();
            var targetOfTargetCastbar = new CastbarHud("DelvUI_targetOfTargetCastbar", targetOfTargetCastbarConfig, "ToT Castbar");
            targetOfTargetCastbar.ParentConfig = _totUnitFrameHud.Config;
            _hudElements.Add(targetOfTargetCastbar);
            _hudElementsUsingTargetOfTarget.Add(targetOfTargetCastbar);

            var focusTargetCastbarConfig = ConfigurationManager.Instance.GetConfigObject<FocusTargetCastbarConfig>();
            var focusTargetCastbar = new CastbarHud("DelvUI_focusTargetCastbar", focusTargetCastbarConfig, "Focus Castbar");
            focusTargetCastbar.ParentConfig = _focusTargetUnitFrameHud.Config;
            _hudElements.Add(focusTargetCastbar);
            _hudElementsUsingFocusTarget.Add(focusTargetCastbar);
        }

        private void CreateStatusEffectsLists()
        {
            var playerBuffsConfig = ConfigurationManager.Instance.GetConfigObject<PlayerBuffsListConfig>();
            var playerBuffs = new StatusEffectsListHud("DelvUI_playerBuffs", playerBuffsConfig, "Buffs");
            playerBuffs.ParentConfig = _playerUnitFrameHud.Config;
            _hudElements.Add(playerBuffs);
            _hudElementsUsingPlayer.Add(playerBuffs);

            var playerDebuffsConfig = ConfigurationManager.Instance.GetConfigObject<PlayerDebuffsListConfig>();
            var playerDebuffs = new StatusEffectsListHud("DelvUI_playerDebuffs", playerDebuffsConfig, "Debufffs");
            playerDebuffs.ParentConfig = _playerUnitFrameHud.Config;
            _hudElements.Add(playerDebuffs);
            _hudElementsUsingPlayer.Add(playerDebuffs);

            var targetBuffsConfig = ConfigurationManager.Instance.GetConfigObject<TargetBuffsListConfig>();
            var targetBuffs = new StatusEffectsListHud("DelvUI_targetBuffs", targetBuffsConfig, "Target Buffs");
            targetBuffs.ParentConfig = _targetUnitFrameHud.Config;
            _hudElements.Add(targetBuffs);
            _hudElementsUsingTarget.Add(targetBuffs);

            var targetDebuffsConfig = ConfigurationManager.Instance.GetConfigObject<TargetDebuffsListConfig>();
            var targetDebuffs = new StatusEffectsListHud("DelvUI_targetDebuffs", targetDebuffsConfig, "Target Debuffs");
            targetDebuffs.ParentConfig = _targetUnitFrameHud.Config;
            _hudElements.Add(targetDebuffs);
            _hudElementsUsingTarget.Add(targetDebuffs);

            var custonEffectsConfig = ConfigurationManager.Instance.GetConfigObject<CustomEffectsListConfig>();
            _customEffectsHud = new CustomEffectsListHud("DelvUI_customEffects", custonEffectsConfig, "Custom Effects");
            _hudElements.Add(_customEffectsHud);
            _hudElementsUsingPlayer.Add(_customEffectsHud);
        }

        private void CreateMiscElements()
        {
            // gcd indicator
            var gcdIndicatorConfig = ConfigurationManager.Instance.GetConfigObject<GCDIndicatorConfig>();
            var gcdIndicator = new GCDIndicatorHud("DelvUI_gcdIndicator", gcdIndicatorConfig, "GCD Indicator");
            _hudElements.Add(gcdIndicator);
            _hudElementsUsingPlayer.Add(gcdIndicator);

            // mp ticker
            var mpTickerConfig = ConfigurationManager.Instance.GetConfigObject<MPTickerConfig>();
            var mpTicker = new MPTickerHud("DelvUI_mpTicker", mpTickerConfig, "MP Ticker");
            _hudElements.Add(mpTicker);
            _hudElementsUsingPlayer.Add(mpTicker);
        }

        public void Draw()
        {
            if (!FontsManager.Instance.DefaultFontBuilt)
            {
                Plugin.UiBuilder.RebuildFonts();
            }

            MouseOverHelper.Instance.Target = null;
            TooltipsHelper.Instance.RemoveTooltip(); // remove tooltip from previous frame

            if (!ShouldBeVisible())
            {
                return;
            }

            ClipRectsHelper.Instance.Update();

            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(ImGui.GetMainViewport().Size);

            var begin = ImGui.Begin(
                "DelvUI_HUD",
                ImGuiWindowFlags.NoTitleBar
              | ImGuiWindowFlags.NoScrollbar
              | ImGuiWindowFlags.AlwaysAutoResize
              | ImGuiWindowFlags.NoBackground
              | ImGuiWindowFlags.NoInputs
              | ImGuiWindowFlags.NoBringToFrontOnFocus
            );

            if (!begin)
            {
                ImGui.End();
                return;
            }

            _hudHelper.Update();

            if (UpdateJob())
            {
                // not the cleanest way of doing this
                // we should probably have some class that checks for the 
                // player's job changing and then firing an event
                // however i didn't want to deal with possible race conditions
                // on the hud manager so for now i'm taking the lazy apporach
                ConfigurationManager.Instance.UpdateCurrentProfile();
            }

            AssignActors();

            // show only castbar during quest events
            if (ShouldOnlyShowCastbar())
            {
                _playerCastbarHud?.Draw(_origin);

                ImGui.End();
                return;
            }

            // grid
            if (_gridConfig is not null && _gridConfig.Enabled)
            {
                DraggablesHelper.DrawGrid(_gridConfig, _selectedElement?.GetConfig());
            }

            bool isHudLocked = ConfigurationManager.Instance.LockHUD;


            // general elements
            foreach (var element in _hudElements)
            {
                if (element != _selectedElement && !_hudHelper.IsElementHidden(element))
                {
                    element.Draw(_origin);
                }
            }

            // job hud
            if (_jobHud != null && _jobHud.Config.Enabled && _jobHud != _selectedElement)
            {
                if (!_hudHelper.IsElementHidden(_jobHud))
                {
                    _jobHud.Draw(_origin);
                }
            }

            // selected
            if (_selectedElement != null)
            {
                _selectedElement.Draw(_origin);
            }

            // tooltip
            TooltipsHelper.Instance.Draw();

            ImGui.End();
        }

        protected unsafe bool ShouldBeVisible()
        {
            if (!ConfigurationManager.Instance.ShowHUD || Plugin.ClientState.LocalPlayer == null)
            {
                return false;
            }

            var parameterWidget = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("_ParameterWidget", 1);
            var fadeMiddleWidget = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("FadeMiddle", 1);

            var paramenterVisible = parameterWidget != null && parameterWidget->IsVisible;
            var fadeMiddleVisible = fadeMiddleWidget != null && fadeMiddleWidget->IsVisible;

            return paramenterVisible && !fadeMiddleVisible;
        }

        protected bool ShouldOnlyShowCastbar()
        {
            // when in quest dialogs and events, hide everything except castbars
            // this includes talking to npcs or interacting with quest related stuff
            if (Plugin.Condition[ConditionFlag.OccupiedInQuestEvent] ||
                Plugin.Condition[ConditionFlag.OccupiedInEvent])
            {
                // we have to wait a bit to avoid weird flickering when clicking shiny stuff
                // we hide delvui after half a second passed in this state
                // interestingly enough, default hotbars seem to do something similar
                var time = ImGui.GetTime();
                if (_occupiedInQuestStartTime > 0)
                {
                    if (time - _occupiedInQuestStartTime > 0.5)
                    {
                        return true;
                    }
                }
                else
                {
                    _occupiedInQuestStartTime = time;
                }
            }
            else
            {
                _occupiedInQuestStartTime = -1;
            }

            return false;
        }

        private bool UpdateJob()
        {
            var player = Plugin.ClientState.LocalPlayer;
            if (player is null)
            {
                return false;
            }

            var newJobId = player.ClassJob.Id;
            if (_jobHud != null && _jobHud.Config.JobId == newJobId)
            {
                return false;
            }

            JobConfig? config = null;

            // unsupported jobs
            if (_unsupportedJobsMap.ContainsKey(newJobId) && _unsupportedJobsMap.TryGetValue(newJobId, out var type))
            {
                config = (JobConfig)Activator.CreateInstance(type)!;
                _jobHud = new JobHud(type.FullName!, config);
            }

            // supported jobs
            if (_jobsMap.TryGetValue(newJobId, out var types))
            {
                config = (JobConfig)ConfigurationManager.Instance.GetConfigObjectForType(types.ConfigType);
                _jobHud = (JobHud)Activator.CreateInstance(types.HudType, types.HudType.FullName, config, types.DisplayName)!;
                _jobHud.SelectEvent += OnDraggableElementSelected;
            }

            return true;
        }

        private void AssignActors()
        {
            // player
            var player = Plugin.ClientState.LocalPlayer;
            foreach (var element in _hudElementsUsingPlayer)
            {
                element.Actor = player;

                if (_jobHud != null)
                {
                    _jobHud.Actor = player;
                }
            }

            // target
            var target = Plugin.TargetManager.SoftTarget ?? Plugin.TargetManager.Target;
            foreach (var element in _hudElementsUsingTarget)
            {
                element.Actor = target;

                if (_customEffectsHud != null)
                {
                    _customEffectsHud.TargetActor = target;
                }
            }

            // target of target
            var targetOfTarget = Utils.FindTargetOfTarget(target, player, Plugin.ObjectTable);
            foreach (var element in _hudElementsUsingTargetOfTarget)
            {
                element.Actor = targetOfTarget;
            }

            // focus
            var focusTarget = Plugin.TargetManager.FocusTarget;
            foreach (var element in _hudElementsUsingFocusTarget)
            {
                element.Actor = focusTarget;
            }

            // player mana bar
            if (_jobHud != null && _playerManaBarHud != null && !_jobHud.Config.UseDefaultPrimaryResourceBar)
            {
                _playerManaBarHud.ResourceType = PrimaryResourceTypes.None;
            }
        }

        protected void CreateJobsMap()
        {
            _jobsMap = new Dictionary<uint, JobHudTypes>()
            {
                // tanks
                [JobIDs.PLD] = new JobHudTypes(typeof(PaladinHud), typeof(PaladinConfig), "Paladin HUD"),
                [JobIDs.WAR] = new JobHudTypes(typeof(WarriorHud), typeof(WarriorConfig), "Warrior HUD"),
                [JobIDs.DRK] = new JobHudTypes(typeof(DarkKnightHud), typeof(DarkKnightConfig), "Dark Knight HUD"),
                [JobIDs.GNB] = new JobHudTypes(typeof(GunbreakerHud), typeof(GunbreakerConfig), "Gunbreaker HUD"),

                // healers
                [JobIDs.WHM] = new JobHudTypes(typeof(WhiteMageHud), typeof(WhiteMageConfig), "White Mage HUD"),
                [JobIDs.SCH] = new JobHudTypes(typeof(ScholarHud), typeof(ScholarConfig), "Scholar HUD"),
                [JobIDs.AST] = new JobHudTypes(typeof(AstrologianHud), typeof(AstrologianConfig), "Astrologian HUD"),

                // melee
                [JobIDs.MNK] = new JobHudTypes(typeof(MonkHud), typeof(MonkConfig), "Monk HUD"),
                [JobIDs.DRG] = new JobHudTypes(typeof(DragoonHud), typeof(DragoonConfig), "Dragoon HUD"),
                [JobIDs.NIN] = new JobHudTypes(typeof(NinjaHud), typeof(NinjaConfig), "Ninja HUD"),
                [JobIDs.SAM] = new JobHudTypes(typeof(SamuraiHud), typeof(SamuraiConfig), "Samurai HUD"),

                // ranged
                [JobIDs.BRD] = new JobHudTypes(typeof(BardHud), typeof(BardConfig), "Bard HUD"),
                [JobIDs.MCH] = new JobHudTypes(typeof(MachinistHud), typeof(MachinistConfig), "Mechanic HUD"),
                [JobIDs.DNC] = new JobHudTypes(typeof(DancerHud), typeof(DancerConfig), "Dancer HUD"),

                // casters
                [JobIDs.BLM] = new JobHudTypes(typeof(BlackMageHud), typeof(BlackMageConfig), "Black Mage HUD"),
                [JobIDs.SMN] = new JobHudTypes(typeof(SummonerHud), typeof(SummonerConfig), "Summoner HUD"),
                [JobIDs.RDM] = new JobHudTypes(typeof(RedMageHud), typeof(RedMageConfig), "Red Mage HUD")
            };

            _unsupportedJobsMap = new Dictionary<uint, Type>()
            {
                [JobIDs.BLU] = typeof(BlueMageConfig),

                // base jobs
                [JobIDs.GLD] = typeof(GladiatorConfig),
                [JobIDs.MRD] = typeof(MarauderConfig),
                [JobIDs.PGL] = typeof(PugilistConfig),
                [JobIDs.LNC] = typeof(LancerConfig),
                [JobIDs.ROG] = typeof(RogueConfig),
                [JobIDs.ARC] = typeof(ArcherConfig),
                [JobIDs.THM] = typeof(ThaumaturgeConfig),
                [JobIDs.ACN] = typeof(ArcanistConfig),
                [JobIDs.CNJ] = typeof(ConjurerConfig),

                // crafters
                [JobIDs.CRP] = typeof(CarpenterConfig),
                [JobIDs.BSM] = typeof(BlacksmithConfig),
                [JobIDs.ARM] = typeof(ArmorerConfig),
                [JobIDs.GSM] = typeof(GoldsmithConfig),
                [JobIDs.LTW] = typeof(LeatherworkerConfig),
                [JobIDs.WVR] = typeof(WeaverConfig),
                [JobIDs.ALC] = typeof(AlchemistConfig),
                [JobIDs.CUL] = typeof(CulinarianConfig),

                // gatherers
                [JobIDs.MIN] = typeof(MinerConfig),
                [JobIDs.BOT] = typeof(BotanistConfig),
                [JobIDs.FSH] = typeof(FisherConfig),
            };
        }
    }

    internal struct JobHudTypes
    {
        public Type HudType;
        public Type ConfigType;
        public string DisplayName;

        public JobHudTypes(Type hudType, Type configType, string displayName)
        {
            HudType = hudType;
            ConfigType = configType;
            DisplayName = displayName;
        }
    }

    internal static class HUDConstants
    {
        internal static int BaseHUDOffsetY = (int)(ImGui.GetMainViewport().Size.Y * 0.3f);
        internal static int UnitFramesOffsetX = 160;
        internal static int PlayerCastbarY = BaseHUDOffsetY - 13;
        internal static int JobHudsBaseY = PlayerCastbarY - 14;
        internal static Vector2 DefaultBigUnitFrameSize = new Vector2(270, 50);
        internal static Vector2 DefaultSmallUnitFrameSize = new Vector2(120, 20);
        internal static Vector2 DefaultStatusEffectsListSize = new Vector2(292, 82);
    }
}
