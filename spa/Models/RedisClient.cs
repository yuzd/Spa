namespace spa.Models
{
    public class RedisClient
    {
        public static string Get(string key)
        {
            return RedisHelper.Get(key);
        }

        public static void Set(string key, string vaue, int senconds = -1)
        {
            RedisHelper.Set(key, vaue, senconds);
        }
    }
}