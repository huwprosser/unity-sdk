﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using UnityEngine;

namespace StatsigUnity
{
    internal class PersistentStore
    {
        const string stableIDKey = "statsig::stableID";
        const string userValuesKeyPrefix = "statsig::userValues-";
        const string logEventsKey = "statsig::logEvents"; // TODO: use this

        internal string stableID;
        Dictionary<string, FeatureGate> _gates;
        Dictionary<string, DynamicConfig> _configs;
        Dictionary<string, Layer> _layers;
        private string currentUserCacheKey;
        private string userHash;
        private long? time;
        private Dictionary<string, string> derivedFields;

        internal PersistentStore(StatsigUser user)
        {
            _gates = new Dictionary<string, FeatureGate>();
            _configs = new Dictionary<string, DynamicConfig>();
            _layers = new Dictionary<string, Layer>();
            time = null;
            derivedFields = null;
            userHash = null;

            stableID = PlayerPrefs.GetString(stableIDKey, null);
            if (stableID == null)
            {
                stableID = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(stableIDKey, stableID);
            }

            currentUserCacheKey = getUserValueKey(user);
            var cachedValues = PlayerPrefs.GetString(currentUserCacheKey, null);

            try
            {
                ParseAndSaveInitResponse(cachedValues);
            }
            catch (Exception)
            {
                PlayerPrefs.DeleteKey(currentUserCacheKey);
            }

            PlayerPrefs.Save();
        }

        internal FeatureGate getGate(string gateName)
        {
            _gates.TryGetValue(gateName, out var gate);
            return gate;
        }

        internal DynamicConfig getConfig(string configName)
        {
            _configs.TryGetValue(configName, out var config);
            return config;
        }

        internal Layer getLayer(string layerName)
        {
            _layers.TryGetValue(layerName, out var layer);
            return layer;
        }

        internal void updateUserValues(StatsigUser user, string values)
        {
            try
            {
                var cacheKey = getUserValueKey(user);
                if (cacheKey == currentUserCacheKey)
                {
                    ParseAndSaveInitResponse(values);
                }
                PlayerPrefs.SetString(cacheKey, values);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
            }
        }

        string getUserValueKey(StatsigUser user)
        {
            var result = $"{userValuesKeyPrefix}userID:{user.UserID};stableID:{stableID}";
            if (user.CustomIDs == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, string> entry in user.CustomIDs)
            {
                result += $";{entry.Key}:${entry.Value}";

            }
            return result;
        }

        internal long? getSinceTime(StatsigUser user)
        {
            if (user.ToHash() != userHash)
            {
                return null;
            }
            return time;
        }

        internal Dictionary<string, string> getDerivedFields(StatsigUser user)
        {
            if (user.ToHash() != userHash)
            {
                return null;
            }
            return derivedFields;
        }

        void ParseAndSaveInitResponse(string responseJson)
        {
            var gates = new Dictionary<string, FeatureGate>();
            var configs = new Dictionary<string, DynamicConfig>();
            var layers = new Dictionary<string, Layer>();
            var response = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(responseJson);
            JToken objVal;

            if (response.TryGetValue("feature_gates", out objVal))
            {
                var gateMap = objVal.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
                foreach (var kv in gateMap)
                {
                    gates[kv.Key] = FeatureGate.FromJObject(kv.Key, kv.Value as JObject);
                }
                _gates = gates;
            }

            if (response.TryGetValue("dynamic_configs", out objVal))
            {
                var configMap = objVal.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
                foreach (var kv in configMap)
                {
                    configs[kv.Key] = DynamicConfig.FromJObject(kv.Key, kv.Value as JObject);
                }
                _configs = configs;
            }

            if (response.TryGetValue("layer_configs", out objVal))
            {
                var layerMap = objVal.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
                foreach (var kv in layerMap)
                {
                    layers[kv.Key] = Layer.FromJObject(kv.Key, kv.Value as JObject);
                }
                _layers = layers;
            }

            if (response.TryGetValue("time", out objVal))
            {
                time = objVal.ToObject<long?>() ?? null;
            }

            if (response.TryGetValue("derived_fields", out objVal))
            {
                derivedFields = objVal.ToObject<Dictionary<string, string>>() ?? null;
            }

            if (response.TryGetValue("user_hash", out objVal))
            {
                userHash = objVal.ToObject<string>() ?? null;
            }
        }
    }
}
