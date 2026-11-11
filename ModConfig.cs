using StardewModdingAPI;

namespace ATLuckyPet
{
    internal sealed class ModConfig
    {
        /// <summary>Bật/tắt luck 100%.</summary>
        public bool LuckEnabled { get; set; } = true;

        /// <summary>Bật/tắt chế độ tự đi vuốt ve vật nuôi.</summary>
        public bool AutoPetEnabled { get; set; } = false;

        /// <summary>Phím bật/tắt luck.</summary>
        public SButton ToggleLuckKey { get; set; } = SButton.F6;

        /// <summary>Phím bật/tắt tự vuốt ve vật nuôi.</summary>
        public SButton ToggleAutoPetKey { get; set; } = SButton.F7;

        /// <summary>Giá trị luck hằng ngày (game tối đa bình thường là 0.1).</summary>
        public float LuckValue { get; set; } = 0.1f;

        /// <summary>Tốc độ di chuyển (pixel mỗi tick).</summary>
        public float MoveSpeed { get; set; } = 4f;
    }
}
