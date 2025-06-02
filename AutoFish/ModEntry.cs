using System;
using System.Collections.Generic;
using System.Linq;
using GenericModConfigMenu;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace AutoFish;

public class ModEntry : Mod
{
    /// <summary>
    /// 正在捕捉宝箱。
    /// </summary>
    private bool _catching;

    /// <summary>
    /// 配置文件。
    /// </summary>
    private ModConfig _config = null!;

    /// <summary>
    /// 当前鼠标状态的反射字段。
    /// </summary>
    private IReflectedField<MouseState>? _currentMouseState;

    /// <summary>
    /// 是否处于连续钓鱼状态。
    /// </summary>
    private bool _isContinuousFishing;

    public override void Entry(IModHelper helper)
    {
        _config = Helper.ReadConfig<ModConfig>();
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.Display.MenuChanged += OnMenuChanged;
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        // 如果不是打开了钓鱼宝箱菜单，则不处理。
        if (e.NewMenu is not ItemGrabMenu { source: ItemGrabMenu.source_fishingChest } itemGrab)
            return;

        // 如果不处于连续钓鱼状态，不处理钓鱼宝箱。
        if (!_isContinuousFishing)
            return;

        IList<Item> actualInventory = itemGrab.ItemsToGrabMenu.actualInventory;
        for (var index = actualInventory.Count - 1; index >= 0; --index)
        {
            if (Game1.player.addItemToInventoryBool(actualInventory.ElementAt(index)))
                actualInventory.RemoveAt(index);
        }

        if (actualInventory.Count == 0)
            itemGrab.exitThisMenu();
        else
            Monitor.Log("物品栏已满！", LogLevel.Warn);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (configMenu is null)
            return;

        configMenu.Register(
            ModManifest,
            () => _config = new ModConfig(),
            () => Helper.WriteConfig(_config)
        );

        configMenu.AddBoolOption(
            ModManifest,
            name: () => Helper.Translation.Get("maxCastPower.name"),
            getValue: () => _config.maxCastPower,
            setValue: value => _config.maxCastPower = value
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => Helper.Translation.Get("autoHit.name"),
            getValue: () => _config.autoHit,
            setValue: value => _config.autoHit = value
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => Helper.Translation.Get("fastBite.name"),
            getValue: () => _config.fastBite,
            setValue: value => _config.fastBite = value
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => Helper.Translation.Get("catchTreasure.name"),
            getValue: () => _config.catchTreasure,
            setValue: value => _config.catchTreasure = value
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => Helper.Translation.Get("fasterSpeed.name"),
            getValue: () => _config.fasterSpeed,
            setValue: value => _config.fasterSpeed = value
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => Helper.Translation.Get("triggerKeepAutoFish.name"),
            getValue: () => _config.triggerKeepAutoFish,
            setValue: value => _config.triggerKeepAutoFish = value
        );
        configMenu.AddKeybindList(
            ModManifest,
            name: () => Helper.Translation.Get("triggerKeepAutoFish.name"),
            getValue: () => _config.keepAutoFishKey,
            setValue: value => _config.keepAutoFishKey = value
        );
        configMenu.AddBoolOption(
            ModManifest,
            name: () => Helper.Translation.Get("autoLootTreasure.name"),
            getValue: () => _config.autoLootTreasure,
            setValue: value => _config.autoLootTreasure = value
        );
        _currentMouseState = Helper.Reflection.GetField<MouseState>(Game1.input, "_currentMouseState");
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (Game1.player.CurrentTool is FishingRod && _config.triggerKeepAutoFish && _config.keepAutoFishKey.JustPressed())
            _isContinuousFishing = !_isContinuousFishing;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        var player = Game1.player;
        if (!Context.IsWorldReady || player == null)
            return;

        if (_isContinuousFishing && player.Stamina < 0)
        {
            _isContinuousFishing = false;
            return;
        }

        var onPressed = IsOnPressedUseToolButton();

        if (player.CurrentTool is FishingRod fishingRod)
        {
            if (_config.fastBite && fishingRod.timeUntilFishingBite > 0)
                fishingRod.timeUntilFishingBite /= 2; // 快速咬钩

            if (_config.autoHit && fishingRod is
                    { isNibbling: true, isReeling: false, hit: false, pullingOutOfWater: false, fishCaught: false, showingTreasure: false })
                fishingRod.DoFunction(player.currentLocation, 1, 1, 1, player); // 自动咬钩

            if (_isContinuousFishing && player.CanMove && fishingRod is
                {
                    isCasting: false, isTimingCast: false, isFishing: false, isNibbling: false, isReeling: false, hit: false, pullingOutOfWater: false,
                    showingTreasure: false, castedButBobberStillInAir: false,
                })
                Game1.pressUseToolButton();

            if (_config.maxCastPower)
                fishingRod.castingPower = 1;

            //use reflection, which may cause some performance problem.
            if (_config.autoLootFishAndTrash && fishingRod.fishCaught)
            {
                foreach (var vaButton in Game1.options.useToolButton)
                {
                    if (vaButton.key != Keys.None)
                    {
                        Game1.oldKBState = new KeyboardState(
                            vaButton.key
                        );
                    }

                    if (_currentMouseState != null && vaButton.mouseLeft)
                    {
                        var currentMouseStateVal = _currentMouseState.GetValue();
                        _currentMouseState.SetValue(new MouseState(
                            currentMouseStateVal.X,
                            currentMouseStateVal.Y,
                            currentMouseStateVal.ScrollWheelValue,
                            ButtonState.Pressed,
                            currentMouseStateVal.MiddleButton,
                            currentMouseStateVal.RightButton,
                            currentMouseStateVal.XButton1,
                            currentMouseStateVal.XButton2,
                            currentMouseStateVal.HorizontalScrollWheelValue));
                    }
                }

                fishingRod.tickUpdate(Game1.currentGameTime, player);
                Game1.oldKBState = new KeyboardState();
                _currentMouseState?.SetValue(new MouseState());
            }
        }

        if (Game1.activeClickableMenu is BobberBar bar) // 自动小游戏
        {
            var barPos = bar.bobberBarPos;
            var barHeight = bar.bobberBarHeight;
            var barSpeed = bar.bobberBarSpeed;
            var barPosMax = 568 - barHeight;

            var fishPos = bar.bobberPosition;
            var fishTargetPos = bar.bobberTargetPosition;
            if (Math.Abs(fishTargetPos - -1.0f) < 0.01f)
                fishTargetPos = fishPos;

            var treasurePos = bar.treasurePosition;
            var treasureCaught = bar.treasureCaught;
            var hasTreasure = bar.treasure;

            var distanceFromCatching = bar.distanceFromCatching;
            var isBossFish = bar.bossFish;
            _catching = _config.catchTreasure && !isBossFish && hasTreasure && !treasureCaught &&
                        (distanceFromCatching > 0.75 || (_catching && distanceFromCatching > 0.15));

            // 默认加速度
            var deltaSpeed = 0.25f * 0.6f;
            if (bar.bobbers.Contains("(O)691")) // 倒刺钩
                deltaSpeed = 0.25f * 0.3f;

            // 自动钓鱼的加速度
            var autoDeltaSpeed = _config.fasterSpeed ? 0.6f : deltaSpeed;

            var targetPos = _catching ? treasurePos : fishPos;
            var otherPos = _catching ? fishPos : fishTargetPos;
            var offset = Math.Clamp(otherPos - targetPos, -barHeight, barHeight) / 4;

            var targetDisplacement = Math.Clamp(targetPos + offset + 20 - 0.5f * barHeight, 0.0f, barPosMax) - barPos;
            var targetSpeed = GetSpeed(autoDeltaSpeed, targetDisplacement);

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
            _ => throw new ArgumentOutOfRangeException(nameof(targetDisplacement), targetDisplacement, null),
        };
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        _isContinuousFishing = false;
    }
}
