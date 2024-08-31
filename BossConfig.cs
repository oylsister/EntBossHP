using Newtonsoft.Json;

namespace EntBossHP
{
    public class BossConfig
    {
        [JsonProperty(PropertyName = "Breakable")]
        public List<BreakableConfig> BreakableList { get; set; } = new List<BreakableConfig>();

        [JsonProperty(PropertyName = "MathCounter")]
        public List<MathCounterConfig> MathCounterList { get; set; } = new List<MathCounterConfig>();

        [JsonProperty(PropertyName = "HPBar")]
        public List<HPBarConfig> HPBarList { get; set; } = new List<HPBarConfig>();
    }

    public class BreakableConfig
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "breakable")]
        public string Breakable {  get; set; }
    }

    public class MathCounterConfig
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "mathcounter")]
        public string MathCounter { get; set; }

        [JsonProperty(PropertyName = "mathcounter_mode")]
        public int MathCounterMode { get; set; } = 1;
    }

    public class HPBarConfig
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "mathcounter")]
        public string MathCounter { get; set; }

        [JsonProperty(PropertyName = "mathcounter_mode")]
        public int MathCounterMode { get; set; } = 1;

        [JsonProperty(PropertyName = "iterator")]
        public string Iterator { get; set; }

        [JsonProperty(PropertyName = "iterator_mode")]
        public int IteratorMode { get; set; } = 1;

        [JsonProperty(PropertyName = "backup")]
        public string Backup { get; set; }
    }
}
