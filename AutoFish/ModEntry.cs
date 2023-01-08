using System;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace AutoFish
{
    public class ModEntry : Mod
    {
        /// <summary>
        ///     正在捕捉宝箱
        /// </summary>
        private bool _catching;

        /// <summary>
        ///     配置文件
        /// </summary>
        private ModConfig Config = null!;

        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<ModConfig>();
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            configMenu.Register(
                ModManifest,
                () => Config = new ModConfig(),
                () => Helper.WriteConfig(Config)
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("maxCastPower.name"),
                getValue: () => Config.maxCastPower,
                setValue: value => Config.maxCastPower = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("autoHit.name"),
                getValue: () => Config.autoHit,
                setValue: value => Config.autoHit = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("fastBite.name"),
                getValue: () => Config.fastBite,
                setValue: value => Config.fastBite = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("catchTreasure.name"),
                getValue: () => Config.catchTreasure,
                setValue: value => Config.catchTreasure = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("fasterSpeed.name"),
                getValue: () => Config.fasterSpeed,
                setValue: value => Config.fasterSpeed = value
            );
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            var player = Game1.player;
            if (!Context.IsWorldReady || player == null)
                return;

            if (player.CurrentTool is FishingRod fishingRod)
            {
                if (Config.fastBite && fishingRod.timeUntilFishingBite > 0)
                    fishingRod.timeUntilFishingBite /= 2; // 快速咬钩

                if (Config.autoHit)
                    if (fishingRod is { isNibbling: true, isReeling: false, hit: false, pullingOutOfWater: false, fishCaught: false, showingTreasure: false })
                        fishingRod.DoFunction(player.currentLocation, 1, 1, 1, player); // 自动咬钩

                if (Config.maxCastPower)
                    fishingRod.castingPower = 1;
            }

            if (Game1.activeClickableMenu is BobberBar bar) // 自动小游戏
            {
                var barPos = Helper.Reflection.GetField<float>(bar, "bobberBarPos").GetValue();
                var barHeight = Helper.Reflection.GetField<int>(bar, "bobberBarHeight").GetValue();
                var fishPos = Helper.Reflection.GetField<float>(bar, "bobberPosition").GetValue();
                var treasurePos = Helper.Reflection.GetField<float>(bar, "treasurePosition").GetValue();
                var distanceFromCatching = Helper.Reflection.GetField<float>(bar, "distanceFromCatching").GetValue();

                var treasureCaught = Helper.Reflection.GetField<bool>(bar, "treasureCaught").GetValue();
                var hasTreasure = Helper.Reflection.GetField<bool>(bar, "treasure").GetValue();
                var bobberBarSpeed = Helper.Reflection.GetField<float>(bar, "bobberBarSpeed").GetValue();
                var barPosMax = 568 - barHeight;

                var whichBobber = Helper.Reflection.GetField<int>(bar, "whichBobber").GetValue();

                if (Config.catchTreasure && hasTreasure && !treasureCaught && (distanceFromCatching > 0.75 || _catching))
                {
                    _catching = true;
                    fishPos = treasurePos;
                }

                if (_catching && distanceFromCatching < 0.15)
                {
                    _catching = false;
                    fishPos = Helper.Reflection.GetField<float>(bar, "bobberPosition").GetValue();
                }

                // 默认加速度
                var deltaSpeed = 0.25f * 0.6f;
                if (whichBobber == 691) // 倒刺钩
                    deltaSpeed = 0.25f * 0.3f;

                // 自动钓鱼的加速度
                var autoDeltaSpeed = Config.fasterSpeed ? 0.6f : deltaSpeed;
                
                var target = Math.Clamp(fishPos + 20 - 0.5f * barHeight, 0.0f, barPosMax) - barPos;
                var maxTargetDisplacement = Math.Min(target > -barHeight * 0.5f ? target / 2 : target + 0.25f * barHeight, barPosMax - barPos);
                var minTargetDisplacement = Math.Max(target < barHeight * 0.5f ? target / 2 : target - 0.25f * barHeight, -barPos);
                var maxSpeed = GetSpeed(autoDeltaSpeed, maxTargetDisplacement);
                var minSpeed = GetSpeed(autoDeltaSpeed, minTargetDisplacement);
                var onPressed = Game1.oldMouseState.LeftButton == ButtonState.Pressed ||
                                Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.useToolButton) ||
                                (Game1.options.gamepadControls && (Game1.oldPadState.IsButtonDown(Buttons.X) || Game1.oldPadState.IsButtonDown(Buttons.A)));

                bobberBarSpeed += onPressed ? -deltaSpeed : deltaSpeed;
                if (bobberBarSpeed < minSpeed)
                    bobberBarSpeed += autoDeltaSpeed;
                else if (bobberBarSpeed > maxSpeed)
                    bobberBarSpeed -= autoDeltaSpeed;

                Helper.Reflection.GetField<float>(bar, "bobberBarSpeed").SetValue(bobberBarSpeed);
            }
            else
            {
                _catching = false;
            }
        }


        private float GetSpeed(float deltaSpeed, float targetDisplacement)
        {
            return targetDisplacement switch
            {
                > 0 => MathF.Sqrt(2 * deltaSpeed * targetDisplacement),
                0 => 0,
                < 0 => -MathF.Sqrt(2 * deltaSpeed * -targetDisplacement),
                _ => throw new ArgumentOutOfRangeException(nameof(targetDisplacement), targetDisplacement, null)
            };
        }
    }
}