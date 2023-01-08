using System;
namespace AutoFish
{
    public class ModConfig
    {
        /// <summary>
        ///     最大力度投掷
        /// </summary>
        public bool maxCastPower { get; set; } = true;
        /// <summary>
        ///     上钩时自动点击
        /// </summary>
        public bool autoHit { get; set; } = true;
        /// <summary>
        ///     快速上钩
        /// </summary>
        public bool fastBite { get; set; } = false;
        /// <summary>
        ///     捕捉宝箱
        /// </summary>
        public bool catchTreasure { get; set; } = true;
    }
}
