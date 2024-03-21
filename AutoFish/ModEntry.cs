﻿using System;
using System.Collections.Generic;
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
                ModManifest,
                name: () => Helper.Translation.Get("maxCastPower.name"),
                getValue: () => Config.maxCastPower,
                setValue: value => Config.maxCastPower = value
            );
            configMenu.AddBoolOption(
                ModManifest,
                name: () => Helper.Translation.Get("autoHit.name"),
                getValue: () => Config.autoHit,
                setValue: value => Config.autoHit = value
            );
            configMenu.AddBoolOption(
                ModManifest,
                name: () => Helper.Translation.Get("fastBite.name"),
                getValue: () => Config.fastBite,
                setValue: value => Config.fastBite = value
            );
            configMenu.AddBoolOption(
                ModManifest,
                name: () => Helper.Translation.Get("catchTreasure.name"),
                getValue: () => Config.catchTreasure,
                setValue: value => Config.catchTreasure = value
            );
            configMenu.AddBoolOption(
                ModManifest,
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
                var barPos = bar.bobberBarPos;
                var barHeight = bar.bobberBarHeight;
                var barSpeed = bar.bobberBarSpeed;
                var barPosMax = 568 - barHeight;

                var fishPos = bar.bobberPosition;
                var fishTargetPos = bar.bobberTargetPosition;
                if (fishTargetPos == -1.0f)
                    fishTargetPos = fishPos;

                var treasurePos = bar.treasurePosition;
                var treasureCaught = bar.treasureCaught;
                var hasTreasure = bar.treasure;

                var distanceFromCatching = bar.distanceFromCatching;
                var isBossFish = bar.bossFish;
                _catching = Config.catchTreasure && !isBossFish && hasTreasure && !treasureCaught &&
                            (distanceFromCatching > 0.75 || (_catching && distanceFromCatching > 0.15));

                // 默认加速度
                var deltaSpeed = 0.25f * 0.6f;
                if (bar.bobbers.Contains("(O)691")) // 倒刺钩
                    deltaSpeed = 0.25f * 0.3f;

                // 自动钓鱼的加速度
                var autoDeltaSpeed = Config.fasterSpeed ? 0.6f : deltaSpeed;

                var targetPos = _catching ? treasurePos : fishPos;
                var otherPos = _catching ? fishPos : fishTargetPos;
                var offset = Math.Clamp(otherPos - targetPos, -barHeight, barHeight) / 4;

                var targetDisplacement = Math.Clamp(targetPos + offset + 20 - 0.5f * barHeight, 0.0f, barPosMax) - barPos;
                var targetSpeed = GetSpeed(autoDeltaSpeed, targetDisplacement);
                var onPressed = IsOnPressedUseToolButton();

                barSpeed += onPressed ? deltaSpeed : -deltaSpeed;
                if (barSpeed < targetSpeed)
                    barSpeed += autoDeltaSpeed;
                else if (barSpeed > targetSpeed)
                    barSpeed -= autoDeltaSpeed;

                bar.bobberBarSpeed = barSpeed;
            }
            else
            {
                _catching = false;
            }
        }

        private static bool IsOnPressedUseToolButton()
        {
            return Game1.oldMouseState.LeftButton == ButtonState.Pressed ||
                   Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.useToolButton) ||
                   (Game1.options.gamepadControls && (Game1.oldPadState.IsButtonDown(Buttons.X) || Game1.oldPadState.IsButtonDown(Buttons.A)));
        }


        private static float GetSpeed(float deltaSpeed, float targetDisplacement)
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