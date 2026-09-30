using System.Text.Json;

namespace e_Commerce_application.Services
{
    // Product codes the visitor looked at most recently, newest first, kept in the session.
    public static class RecentlyViewed
    {
        private const string SessionKey = "recent";
        private const int Max = 12;

        public static List<int> Get(ISession session)
        {
            var json = session.GetString(SessionKey);
            return string.IsNullOrEmpty(json) ? new List<int>() : JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
        }

        public static void Add(ISession session, int productCode)
        {
            var codes = Get(session);
            codes.Remove(productCode);
            codes.Insert(0, productCode);
            session.SetString(SessionKey, JsonSerializer.Serialize(codes.Take(Max)));
        }
    }
}
