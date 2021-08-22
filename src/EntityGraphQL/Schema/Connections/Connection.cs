using System.Collections.Generic;
using System.ComponentModel;

namespace EntityGraphQL.Schema.Connections
{
    public class Connection<TEntity>
    {
        public Connection(int totalCount, dynamic arguments)
        {
            TotalCount = totalCount;
            PageInfo = new ConnectionPageInfo(totalCount, arguments);
            arguments.totalCount = totalCount;
        }

        [GraphQLNotNull]
        [Description("Edge information about each node in the collection")]
        public IEnumerable<ConnectionEdge<TEntity>> Edges { get; set; }
        [GraphQLNotNull]
        [Description("Total count of items in the collection")]
        public int TotalCount { get; set; }
        [GraphQLNotNull]
        [Description("Information about this page of data")]

        public ConnectionPageInfo PageInfo { get; set; }
    }
}