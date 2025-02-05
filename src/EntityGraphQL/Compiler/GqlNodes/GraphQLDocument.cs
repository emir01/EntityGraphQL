using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Top level result of parsing a GraphQL document.
    /// Contains a list of top level operations defined in the query document. They can either be queries or mutations.
    /// Also contains a list of fragments defined in the query document
    /// e.g.
    /// {
    ///     query Op1 {
    ///         people { name id }
    ///         movies { name released }
    ///     }
    ///     query Op2 {
    ///         ...
    ///     }
    ///     mutation ...
    ///     fragment ...
    /// }
    /// </summary>
    public class GraphQLDocument : IGraphQLNode
    {
        public Expression NextFieldContext { get; set; }
        public IGraphQLNode ParentNode { get; set; }
        public ParameterExpression RootParameter { get; set; }
        public List<BaseGraphQLField> QueryFields { get; protected set; } = new List<BaseGraphQLField>();
        private readonly Func<string, string> fieldNamer;

        /// <summary>
        /// A list of graphql operations. These could be mutations or queries
        /// </summary>
        /// <value></value>
        public List<ExecutableGraphQLStatement> Operations { get; }
        public List<GraphQLFragmentStatement> Fragments { get; set; }

        public GraphQLDocument(Func<string, string> fieldNamer)
        {
            Operations = new List<ExecutableGraphQLStatement>();
            Fragments = new List<GraphQLFragmentStatement>();
            this.fieldNamer = fieldNamer;
        }

        public string Name
        {
            get => "Query Request Root";
        }

        public QueryResult ExecuteQuery<TContext>(TContext context, IServiceProvider services, string operationName = null, ExecutionOptions options = null)
        {
            return ExecuteQueryAsync(context, services, operationName, options).Result;
        }

        /// <summary>
        /// Executes the compiled GraphQL document adding data results into QueryResult.
        /// If no OperationName is supplied the first operation in the query document is executed
        /// </summary>
        /// <param name="context">Instance of the context type of the schema</param>
        /// <param name="services">Service provider used for DI</param>
        /// <param name="operationName">Optional operation name</param>
        /// <returns></returns>
        public async Task<QueryResult> ExecuteQueryAsync<TContext>(TContext context, IServiceProvider services, string operationName, ExecutionOptions options = null)
        {
            // check operation names
            if (Operations.Count > 1 && Operations.Count(o => string.IsNullOrEmpty(o.Name)) > 0)
            {
                throw new EntityGraphQLExecutionException("An operation name must be defined for all operations if there are multiple operations in the request");
            }
            var result = new QueryResult();
            var validator = new GraphQLValidator();
            var op = string.IsNullOrEmpty(operationName) ? Operations.First() : Operations.First(o => o.Name == operationName);

            // execute the selected operation
            result.Data = await op.ExecuteAsync(context, validator, services, Fragments, fieldNamer, options);

            if (validator.Errors.Count > 0)
                result.AddErrors(validator.Errors);

            return result;
        }

        public void AddField(BaseGraphQLField field)
        {
            throw new NotImplementedException();
        }
    }
}