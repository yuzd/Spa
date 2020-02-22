using System.Collections.Generic;
using CSRedis;

namespace spa.Models
{
    public class RedisClient
    {
        private static readonly Dictionary<string, CSRedis.CSRedisClient> client = new Dictionary<string, CSRedisClient>();

        private readonly CSRedis.CSRedisClient _redis;

        public RedisClient(string name)
        {
            if (!client.TryGetValue(name, out var redis))
            {
                _redis = new CSRedisClient(name);
                client.TryAdd(name, _redis);
            }
            else
            {
                _redis = redis;
            }
        }

        public string get(string key)
        {
            return _redis.Get(key);
        }

        public void set(string key, string vaue, int senconds = -1)
        {
            _redis.Set(key, vaue, senconds);
        }
    }

}