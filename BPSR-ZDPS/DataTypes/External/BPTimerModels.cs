using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPSR_ZDPS.DataTypes.External
{
    public class MobsResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("location")]
        public bool Location { get; set; }
        [JsonProperty("map")]
        public string Map { get; set; }
        [JsonProperty("monster_id")]
        public long MonsterId { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("respawn_time")]
        public int RespawnTime { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("uid")]
        public int UID { get; set; }
        [JsonProperty("expand")]
        public MobsResponseExpand Expand { get; set; }
    }

    public class MobsResponseExpand
    {
        [JsonProperty("map")]
        public MobsResponseExpandMap Map { get; set; }
    }

    public class MobsResponseExpandMap
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("region_data")]
        public Dictionary<string, int> RegionData { get; set; }
        [JsonProperty("uid")]
        public int UID { get; set; }
    }

    public class StatusResponse
    {
        [JsonProperty("channel_number")]
        public int ChannelNumber { get; set; }
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("last_hp")]
        public int LastHP { get; set; }
        [JsonProperty("last_update")]
        public string? LastUpdate { get; set; }
        [JsonProperty("update")]
        public string? Update { get; set; }
        [JsonProperty("location_image")]
        public string LocationImage { get; set; }
        [JsonProperty("mob")]
        public string Mob { get; set; }
        [JsonProperty("region")]
        public string Region { get; set; }
    }

    public class BPTimerHpReport
    {
        [JsonProperty("monster_id")]
        public long MonsterId;
        [JsonProperty("hp_pct")]
        public int HpPct;
        [JsonProperty("line")]
        public uint Line;
        [JsonProperty("pos_x", NullValueHandling = NullValueHandling.Ignore)]
        public float? PosX;
        [JsonProperty("pos_y", NullValueHandling = NullValueHandling.Ignore)]
        public float? PosY;
        [JsonProperty("pos_z", NullValueHandling = NullValueHandling.Ignore)]
        public float? PosZ;
        [JsonProperty("account_id", NullValueHandling = NullValueHandling.Ignore)]
        public string? AccountId;
        [JsonProperty("uid", NullValueHandling = NullValueHandling.Ignore)]
        public long? UID;
    }

    public class BPTimerSubscribe
    {
        [JsonProperty("clientId")]
        public string ClientId;
        [JsonProperty("subscriptions")]
        public List<string> Subscriptions;
    }

    public class BPTimerMobHpUpdate
    {
        public string MobId;
        public int Channel;
        public int Hp;
        public string? Location;
    }

    public class BPTimerMobHpUpdateConverter : JsonConverter<BPTimerMobHpUpdate>
    {
        public override BPTimerMobHpUpdate ReadJson(JsonReader reader, Type objectType, BPTimerMobHpUpdate? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JArray arr = JArray.Load(reader);

            return new BPTimerMobHpUpdate
            {
                MobId = (string)arr[0],
                Channel = (int)arr[1],
                Hp = (int)arr[2],
                Location = arr[3].Type == JTokenType.Null ? null : (string)arr[3],
            };
        }

        public override void WriteJson(JsonWriter writer, BPTimerMobHpUpdate? value, JsonSerializer serializer)
        {
            JArray arr = new JArray
            {
                value.MobId,
                value.Channel,
                value.Hp,
                value.Location
            };
        }
    }
}
