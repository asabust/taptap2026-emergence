using System;
using System.IO;
using System.Linq;
using Game.Runtime.Data.Levels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Runtime.Gameplay.Levels
{
    /// <summary>Strict schema decoding, independent of playability. Incomplete drafts are supported.</summary>
    public static class LevelJson
    {
        private static JsonSerializer NewSerializer()
        {
            return JsonSerializer.Create(new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error,
                TypeNameHandling = TypeNameHandling.None,
                MaxDepth = 64
            });
        }

        public static LevelDefinition Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Level JSON is empty.");
            try
            {
                JObject root;
                using (var reader = new JsonTextReader(new StringReader(json)))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    reader.MaxDepth = 64;
                    root = JObject.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                    });
                    if (reader.Read()) throw new FormatException("Unexpected content after level JSON.");
                }
                CheckShape(root);
                var result = root.ToObject<LevelDefinition>(NewSerializer());
                if (result == null) throw new FormatException("Level must be an object.");
                return result;
            }
            catch (JsonException e) { throw new FormatException("Invalid level JSON: " + e.Message, e); }
        }

        public static string Serialize(LevelDefinition level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            var root = JObject.FromObject(level, NewSerializer());
            CheckShape(root);
            return root.ToString(Formatting.Indented);
        }

        public static LevelDefinition DeepCopy(LevelDefinition level)
        {
            return Deserialize(Serialize(level));
        }

        private static void CheckShape(JObject root)
        {
            Fields(root, "schemaVersion,id,name,grid,rules,iterationLimit,cloneLimit,tiles,cells,winConditions");
            Integer(root, "schemaVersion", false);
            if (root.Value<int>("schemaVersion") != LevelIds.SchemaVersion)
                throw new FormatException("Unsupported schemaVersion.");
            Text(root, "id"); Text(root, "name");
            Integer(root, "iterationLimit"); Integer(root, "cloneLimit");
            Object(root, "grid", o => { Fields(o, "width,height"); Integer(o, "width"); Integer(o, "height"); });
            Array(root, "rules", o =>
            {
                Fields(o, "id,parameters"); Text(o, "id");
                string id = (string)o["id"];
                if (id != LevelIds.LonelyDeath && id != LevelIds.CrowdedDeath)
                    throw new FormatException("Unknown rule: " + id);
                // GDD originally reserved parameters; the two current rules have fixed thresholds.
                if (o["parameters"] != null &&
                    (!(o["parameters"] is JObject parameters) || parameters.HasValues))
                    throw new FormatException("Current rules do not accept parameters.");
                o.Remove("parameters");
            });
            Array(root, "tiles", o => { Fields(o, "x,y,type"); Position(o); Text(o, "type");
                Choice(o, "type", LevelIds.Floor, LevelIds.Wall); });
            Array(root, "cells", o => { Fields(o, "x,y,type"); Position(o); Text(o, "type");
                Choice(o, "type", LevelIds.Normal, LevelIds.Player); });
            Object(root, "winConditions", o =>
            {
                Fields(o, "combine,conditions"); Text(o, "combine");
                if (o["combine"] != null) Choice(o, "combine", "all");
                Array(o, "conditions", goal =>
                {
                    Fields(goal, "type,position,positions,operator,value"); Text(goal, "type");
                    Choice(goal, "type", LevelIds.ActorAtPosition, LevelIds.CellCount, LevelIds.CoverPositions);
                    Object(goal, "position", pos => { Fields(pos, "x,y"); Position(pos); });
                    Array(goal, "positions", pos => { Fields(pos, "x,y"); Position(pos); });
                    Text(goal, "operator"); Integer(goal, "value", true, true);
                    if (goal["operator"] != null && goal["operator"].Type != JTokenType.Null)
                        Choice(goal, "operator", ">", "<");
                });
            });
        }

        private static void Fields(JObject obj, string allowed)
        {
            var names = allowed.Split(',');
            foreach (var p in obj.Properties())
                if (!names.Contains(p.Name)) throw new FormatException("Unknown field: " + p.Path);
        }
        private static void Text(JObject o, string name)
        {
            var t = o[name];
            if (t != null && t.Type != JTokenType.Null && t.Type != JTokenType.String)
                throw new FormatException(name + " must be a string.");
        }
        private static void Integer(JObject o, string name, bool optional = true, bool nullable = false)
        {
            var t = o[name];
            if (t == null && optional) return;
            if (t != null && t.Type == JTokenType.Null && nullable) return;
            if (t == null || t.Type != JTokenType.Integer ||
                !int.TryParse(t.ToString(), out _)) throw new FormatException(name + " must be a 32-bit integer.");
        }
        private static void Position(JObject o) { Integer(o, "x", false); Integer(o, "y", false); }
        private static void Choice(JObject o, string name, params string[] choices)
        {
            if (!choices.Contains((string)o[name])) throw new FormatException("Unknown " + name + ": " + o[name]);
        }
        private static void Object(JObject o, string name, Action<JObject> check)
        {
            var t = o[name];
            if (t == null || t.Type == JTokenType.Null) return;
            if (!(t is JObject obj)) throw new FormatException(name + " must be an object.");
            check(obj);
        }
        private static void Array(JObject o, string name, Action<JObject> check)
        {
            var t = o[name];
            if (t == null || t.Type == JTokenType.Null) return;
            if (!(t is JArray array)) throw new FormatException(name + " must be an array.");
            foreach (var item in array)
            {
                if (!(item is JObject obj)) throw new FormatException(name + " entries must be objects.");
                check(obj);
            }
        }
    }
}
