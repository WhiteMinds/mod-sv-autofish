using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace AutoFish
{
    public interface IGenericModConfigMenuApi
    {
        /*********
        ** Methods
        *********/
        /****
        ** Must be called first
        ****/
        /// <summary>Register a mod whose config can be edited through the UI.</summary>
        /// <param name="mod">The mod's manifest.</param>
        /// <param name="reset">Reset the mod's config to its default values.</param>
        /// <param name="save">Save the mod's current config to the <c>config.json</c> file.</param>
        /// <param name="titleScreenOnly">Whether the options can only be edited from the title screen.</param>
        /// <remarks>Each mod can only be registered once, unless it's deleted via <see cref="Unregister"/> before calling this again.</remarks>
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        /// <summary>Add a boolean option at the current position in the form.</summary>
        /// <param name="mod">The mod's manifest.</param>
        /// <param name="getValue">Get the current value from the mod config.</param>
        /// <param name="setValue">Set a new value in the mod config.</param>
        /// <param name="name">The label text to show in the form.</param>
        /// <param name="tooltip">The tooltip text shown when the cursor hovers on the field, or <c>null</c> to disable the tooltip.</param>
        /// <param name="fieldId">The unique field ID for use with <see cref="OnFieldChanged"/>, or <c>null</c> to auto-generate a randomized ID.</param>
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);

        /// <summary>Add a string option at the current position in the form.</summary>
        /// <param name="mod">The mod's manifest.</param>
        /// <param name="getValue">Get the current value from the mod config.</param>
        /// <param name="setValue">Set a new value in the mod config.</param>
        /// <param name="name">The label text to show in the form.</param>
        /// <param name="tooltip">The tooltip text shown when the cursor hovers on the field, or <c>null</c> to disable the tooltip.</param>
        /// <param name="allowedValues">The values that can be selected, or <c>null</c> to allow any.</param>
        /// <param name="formatAllowedValue">Get the display text to show for a value from <paramref name="allowedValues"/>, or <c>null</c> to show the values as-is.</param>
        /// <param name="fieldId">The unique field ID for use with <see cref="OnFieldChanged"/>, or <c>null</c> to auto-generate a randomized ID.</param>
        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string> tooltip = null, string[] allowedValues = null, Func<string, string> formatAllowedValue = null, string fieldId = null);

        /// <summary>Remove a mod from the config UI and delete all its options and pages.</summary>
        /// <param name="mod">The mod's manifest.</param>
        void Unregister(IManifest mod);
    }
    public class ModEntry : Mod
    {
        private ModConfig Config;
        private bool catching = false;

        public override void Entry(IModHelper helper)
        {
            Config = this.Helper.ReadConfig<ModConfig>();
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            Farmer player = Game1.player;
            if (!Context.IsWorldReady || player == null)
                return;

            if (player.CurrentTool is FishingRod currentTool)
            {
                if (Config.fastBite && currentTool.timeUntilFishingBite > 0)
                    currentTool.timeUntilFishingBite /= 2; // 快速咬钩

                if (Config.autoHit && currentTool.isNibbling && !currentTool.isReeling && !currentTool.hit && !currentTool.pullingOutOfWater && !currentTool.fishCaught)
                    currentTool.DoFunction(player.currentLocation, 1, 1, 1, player); // 自动咬钩

                if (Config.maxCastPower)
                    currentTool.castingPower = 1;
            }

            if (Game1.activeClickableMenu is BobberBar bar) // 自动小游戏
            {
                float barPos = Helper.Reflection.GetField<float>(bar, "bobberBarPos").GetValue();
                float barHeight = Helper.Reflection.GetField<int>(bar, "bobberBarHeight").GetValue();
                float fishPos = Helper.Reflection.GetField<float>(bar, "bobberPosition").GetValue();
                float treasurePos = Helper.Reflection.GetField<float>(bar, "treasurePosition").GetValue();
                float distanceFromCatching = Helper.Reflection.GetField<float>(bar, "distanceFromCatching").GetValue();

                bool treasureCaught = Helper.Reflection.GetField<bool>(bar, "treasureCaught").GetValue();
                bool hasTreasure = Helper.Reflection.GetField<bool>(bar, "treasure").GetValue();
                float bobberBarSpeed = Helper.Reflection.GetField<float>(bar, "bobberBarSpeed").GetValue();
                float barPosMax = 568 - barHeight;

                float min = barPos + barHeight / 4,
                    max = barPos + barHeight / 1.5f;

                if (Config.catchTreasure && hasTreasure && !treasureCaught && (distanceFromCatching > 0.75 || catching))
                {
                    catching = true;
                    fishPos = treasurePos;
                }
                if (catching && distanceFromCatching < 0.15)
                {
                    catching = false;
                    fishPos = Helper.Reflection.GetField<float>(bar, "bobberPosition").GetValue();
                }

                if (fishPos < min)
                {
                    bobberBarSpeed -= 0.35f + (min - fishPos) / 20;
                    Helper.Reflection.GetField<float>(bar, "bobberBarSpeed").SetValue(bobberBarSpeed);
                }
                else if (fishPos > max)
                {
                    bobberBarSpeed += 0.35f + (fishPos - max) / 20;
                    Helper.Reflection.GetField<float>(bar, "bobberBarSpeed").SetValue(bobberBarSpeed);
                }
                else
                {
                    float target = 0.1f;
                    if (bobberBarSpeed > target)
                    {
                        bobberBarSpeed -= 0.1f + (bobberBarSpeed - target) / 25;
                        if (barPos + bobberBarSpeed > barPosMax)
                            bobberBarSpeed /= 2; // 减小触底反弹
                        if (bobberBarSpeed < target)
                            bobberBarSpeed = target;
                    }
                    else
                    {
                        bobberBarSpeed += 0.1f + (target - bobberBarSpeed) / 25;
                        if (barPos + bobberBarSpeed < 0)
                            bobberBarSpeed /= 2; // 减小触顶反弹
                        if (bobberBarSpeed > target)
                            bobberBarSpeed = target;
                    }
                    Helper.Reflection.GetField<float>(bar, "bobberBarSpeed").SetValue(bobberBarSpeed);
                }
            }
            else
            {
                catching = false;
            }
        }
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Max Cast Power",
                tooltip: () => "Turn casting the rod with Max Power On or Off",
                getValue: () => Config.maxCastPower,
                setValue: value => Config.maxCastPower = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Auto Hit",
                tooltip: () => "Turn Auto Hit On or Off",
                getValue: () => Config.autoHit,
                setValue: value => Config.autoHit = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Fast Bite",
                tooltip: () => "Turn Fast Bite On or Off",
                getValue: () => Config.fastBite,
                setValue: value => Config.fastBite = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Catch Treasure",
                tooltip: () => "Turn Catching Treasure while Fishing On or Off",
                getValue: () => Config.catchTreasure,
                setValue: value => Config.catchTreasure = value
            );
        }
    }
}
