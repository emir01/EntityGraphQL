using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests.ConnectionPaging
{
    public class CursorSerializationTests
    {
        [Fact]
        public void TestSerializeAndDeserialize()
        {
            const int val = 0;
            var cursor = CursorHelper.SerializeCursor(val);
            var valBack = CursorHelper.DeserializeCursor(cursor);

            Assert.NotNull(valBack);
            Assert.Equal(val, valBack);
        }

        [Fact]
        public void TestSerializeAndDeserializeLarge()
        {
            const int val = int.MaxValue;
            var cursor = CursorHelper.SerializeCursor(val);
            var valBack = CursorHelper.DeserializeCursor(cursor);

            Assert.NotNull(valBack);
            Assert.Equal(val, valBack);
        }
    }
}