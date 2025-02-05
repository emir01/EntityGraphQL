using Xunit;
using System.Collections.Generic;
using EntityGraphQL.Schema;
using System.Linq;
using static EntityGraphQL.Schema.ArgumentHelper;
using Microsoft.Extensions.DependencyInjection;
using System;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Tests
{
    public class ServiceFieldTests
    {
        [Fact]
        public void TestServicesAtRoot()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination").AddAllFields();

            schema.ReplaceField("projects",
                new PagerArgs { page = 1, pagesize = 10 },
                (db, p) => WithService((EntityPager pager) => pager.PageProjects(db, p)),
                "Pagination. [defaults: page = 1, pagesize = 10]");

            var gql = new QueryRequest
            {
                Query = @"{ projects { total items { id } } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>()
            };
            var serviceCollection = new ServiceCollection();
            var pager = new EntityPager();
            serviceCollection.AddSingleton(pager);

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, pager.CallCount);
        }

        [Fact]
        public void TestServicesNonRoot()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination").AddAllFields();

            schema.Type<Person>().ReplaceField("projects",
                new { page = 1, pagesize = 10 },
                (person, p) => WithService((EntityPager pager) => pager.PageProjects(person.Projects, p)),
                "Pagination. [defaults: page = 1, pagesize = 10]");

            var gql = new QueryRequest
            {
                Query = @"{ people { projects { total items { id } } } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>(),
                People = new List<Person>
                {
                    new Person
                    {
                        Projects = new List<Project>()
                    }
                }
            };
            var serviceCollection = new ServiceCollection();
            EntityPager pager = new EntityPager();
            serviceCollection.AddSingleton(pager);

            // Builds - (ctx, pager, args) => ctx.People
            //              .Select(p => new { projects = p.Projects })
            //              .ToList()
            //              .Select(p => new { pager.PageProjects(p.projects, args) })

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, pager.CallCount);
            dynamic person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Assert.Single(person.GetType().GetFields());
            Assert.Equal("projects", person.GetType().GetFields()[0].Name);
        }

        [Fact]
        public void TestServicesNonRootDeeper()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("config",
                (p) => WithService((ConfigService srv) => srv.Get(p.Id)),
                "Get project config");

            var gql = new QueryRequest
            {
                Query = @"{ people { projects { config { type } } } }"
            };

            var context = new TestDataContext
            {
                People = new List<Person>
                        {
                            new Person
                            {
                                Projects = new List<Project>
                                {
                                    new Project
                                    {
                                        Id = 4,
                                    }
                                }
                            }
                        }
            };
            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            // Builds - (ctx, srv, args) => ctx.People
            //              .Select(p => new {
            //                  projects = p.Projects.Select(p => new {
            //                      Id = p.Id
            //                  })
            //              })
            //              .ToList()
            //              .Select(p => new {
            //                  projects = p.Projects.Select(p => new {
            //                      config = wrapservice...(p.Id)
            //                  })
            //              })

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Assert.Single(person.GetType().GetFields());
            Assert.Equal("projects", person.GetType().GetFields()[0].Name);
            dynamic project = Enumerable.ElementAt((dynamic)person.projects, 0);
            Assert.Equal("config", project.GetType().GetFields()[0].Name);
            Assert.Equal(1, srv.CallCount);
        }

        [Fact]
        public void TestServicesNonRootWithOtherFields()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination").AddAllFields();

            schema.Type<Person>().ReplaceField("projects",
                new { page = 1, pagesize = 10 },
                (person, p) => WithService((EntityPager pager) => pager.PageProjects(person.Projects, p)),
                "Pagination. [defaults: page = 1, pagesize = 10]");

            var gql = new QueryRequest
            {
                Query = @"{ people { projects { total items { id } } name } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>(),
                People = new List<Person>
                        {
                            new Person
                            {
                                Name = "Alyssa",
                                Projects = new List<Project>()
                            }
                        }
            };
            var serviceCollection = new ServiceCollection();
            EntityPager pager = new EntityPager();
            serviceCollection.AddSingleton(pager);

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, pager.CallCount);
            var person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("projects", person.GetType().GetFields()[0].Name);
            Assert.Equal("name", person.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void TestServicesNonRootWithOtherFieldsAndRelation()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination").AddAllFields();

            schema.Type<Person>().ReplaceField("projects",
                new { page = 1, pagesize = 10 },
                (person, p) => WithService((EntityPager pager) => pager.PageProjects(person.Projects, p)),
                "Pagination. [defaults: page = 1, pagesize = 10]");

            var gql = new QueryRequest
            {
                Query = @"{ people { projects { total items { id } } manager { name } } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>(),
                People = new List<Person>
                        {
                            new Person
                            {
                                Name = "Alyssa",
                                Projects = new List<Project>(),
                                Manager = new Person
                                {
                                    Name = "Jennifer"
                                }
                            }
                        }
            };
            var serviceCollection = new ServiceCollection();
            EntityPager pager = new EntityPager();
            serviceCollection.AddSingleton(pager);

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, pager.CallCount);
            var person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("projects", person.GetType().GetFields()[0].Name);
            Assert.Equal("manager", person.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void TestServicesNonRootWithOtherFieldsAndRelation2()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination").AddAllFields();

            schema.Type<Person>().AddField("age",
                // use a filed not another relation/entity
                (person) => WithService((AgeService ager) => ager.GetAge(person.Birthday)),
                "Persons age");

            var gql = new QueryRequest
            {
                Query = @"{ people { age manager { name } } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>(),
                People = new List<Person>
                        {
                            new Person
                            {
                                Name = "Alyssa",
                                Projects = new List<Project>(),
                                Manager = new Person
                                {
                                    Name = "Jennifer"
                                }
                            }
                        }
            };
            var serviceCollection = new ServiceCollection();
            var ager = new AgeService();
            serviceCollection.AddSingleton(ager);

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, ager.CallCount);
            var person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("age", person.GetType().GetFields()[0].Name);
            Assert.Equal("manager", person.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void TestServicesNonRootWithOtherFieldsAndRelationNotEnumerable()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination").AddAllFields();

            schema.Type<Person>().AddField("age",
                // use a filed not another relation/entity
                (person) => WithService((AgeService ager) => ager.GetAge(person.Birthday)),
                "Persons age");

            var gql = new QueryRequest
            {
                // the service field (age) is on a 1-1 relation not 1-Many so we don't build a .Select
                Query = @"{ people { manager { name age } } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>(),
                People = new List<Person>
                        {
                            new Person
                            {
                                Name = "Alyssa",
                                Projects = new List<Project>(),
                                Manager = new Person
                                {
                                    Name = "Jennifer"
                                }
                            }
                        }
            };
            var serviceCollection = new ServiceCollection();
            var ager = new AgeService();
            serviceCollection.AddSingleton(ager);

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, ager.CallCount);
            var person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Type resultType = person.GetType();
            Assert.Single(resultType.GetFields());
            Assert.Equal("manager", resultType.GetFields()[0].Name);
            Assert.Equal("name", person.manager.GetType().GetFields()[0].Name);
            Assert.Equal("age", person.manager.GetType().GetFields()[1].Name);

        }

        [Fact]
        public void TestServicesReconnectToSchemaContext()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            // Linking a type from a service back to the schema context
            schema.Type<User>().ReplaceField("projects",
                (user) => WithService((TestDataContext db) => db.Projects.Where(p => p.Owner.Id == user.Id)),
                "Peoples projects");

            schema.ReplaceField("user", ctx => WithService((UserService users) => users.GetUser()), "Get current user");

            var gql = new QueryRequest
            {
                Query = @"{ user { id projects { id } } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>(),
                People = new List<Person>
                        {
                            new Person
                            {
                                Projects = new List<Project>()
                            }
                        },
            };
            var serviceCollection = new ServiceCollection();
            UserService userService = new UserService();
            serviceCollection.AddSingleton(userService);
            serviceCollection.AddSingleton(context);

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, userService.CallCount);
            Assert.Equal(2, res.Data["user"].GetType().GetFields().Count());
            Assert.Equal("id", res.Data["user"].GetType().GetFields().ElementAt(0).Name);
            Assert.Equal("projects", res.Data["user"].GetType().GetFields().ElementAt(1).Name);
        }

        [Fact]
        public void TestServicesReconnectToSchemaContext2()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            // Linking a type from a service back to the schema context
            schema.Type<User>().ReplaceField("projects",
                (user) => WithService((TestDataContext db) => db.Projects.Where(p => p.Owner.Id == user.Id)),
                "Peoples projects");

            schema.ReplaceField("users", ctx => WithService((UserService users) => users.GetUsers()), "Get current user");

            var gql = new QueryRequest
            {
                Query = @"{ users { id projects { id } } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>(),
                People = new List<Person>
                        {
                            new Person
                            {
                                Projects = new List<Project>()
                            }
                        },
            };
            var serviceCollection = new ServiceCollection();
            UserService userService = new();
            serviceCollection.AddSingleton(userService);
            serviceCollection.AddSingleton(context);

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, userService.CallCount);
            dynamic users = res.Data["users"];
            Assert.Equal(2, users[0].GetType().GetFields().Length);
            Assert.Equal("id", Enumerable.ElementAt(users[0].GetType().GetFields(), 0).Name);
            Assert.Equal("projects", Enumerable.ElementAt(users[0].GetType().GetFields(), 1).Name);
        }

        [Fact]
        public void TestSelectFromServiceDeepInLists()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Settings>("Settings", "The settings").AddAllFields();
            schema.Type<Task>().AddField("settings",
                (t) => WithService((SettingsService settings) => settings.Get(t.Id, false)),
                "Task settings").IsNullable(false);

            var gql = new QueryRequest
            {
                Query = @"query {
          projects {
            tasks {
              settings {
                allowComments
              }
              id # the service field below requires id. Make sure we don't select it twice
            }
          }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = new List<Task> { new Task() }
                            }
                        },
            };
            var serviceCollection = new ServiceCollection();
            var settings = new SettingsService();
            serviceCollection.AddSingleton(settings);

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            dynamic project = Enumerable.ElementAt((dynamic)res.Data["projects"], 0);
            Type projectType = project.GetType();
            Assert.Null(res.Errors);
            Assert.Equal(1, settings.CallCount);
            Assert.Single(projectType.GetFields());
            Assert.Equal("tasks", projectType.GetFields()[0].Name);
            Assert.Equal("settings", project.tasks.GetType().GetGenericArguments()[0].GetFields()[0].Name);
            Assert.Equal("allowComments", project.tasks[0].settings.GetType().GetFields()[0].Name);
        }

        [Fact]
        public void TestComplexFieldWithServiceField()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Settings>("Settings", "The settings").AddAllFields();
            schema.Type<Project>().AddField("settings",
                (t) => WithService((SettingsService settings) => settings.Get(t.Id, false)),
                "Project settings").IsNullable(false);
            schema.Type<Project>().AddField("totalTasks", p => p.Tasks.Count(), "Total tasks");

            var gql = new QueryRequest
            {
                Query = @"query {
          projects {
            totalTasks
            settings {
              allowComments
            }
          }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = new List<Task> { new Task() }
                            }
                        },
            };
            var serviceCollection = new ServiceCollection();
            var settings = new SettingsService();
            serviceCollection.AddSingleton(settings);

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            dynamic project = Enumerable.ElementAt((dynamic)res.Data["projects"], 0);
            Type projectType = project.GetType();
            Assert.Null(res.Errors);
            Assert.Equal(1, settings.CallCount);
            Assert.Equal(2, projectType.GetFields().Count());
            Assert.Equal("totalTasks", projectType.GetFields()[0].Name);
            Assert.Equal("settings", projectType.GetFields()[1].Name);
            Assert.Equal("allowComments", project.settings.GetType().GetFields()[0].Name);
        }

        [Fact]
        public void TestComplexFieldInObjectWithServiceField()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Settings>("Settings", "The settings").AddAllFields();
            schema.Type<Project>().AddField("settings",
                (t) => WithService((SettingsService settings) => settings.Get(t.Id, false)),
                "Project settings").IsNullable(false);
            schema.Type<Project>().AddField("totalTasks", p => p.Tasks.Count(), "Total tasks");
            schema.Type<Person>().AddField("managerId", p => p.Manager.Id, "Persons managers ID");

            var gql = new QueryRequest
            {
                Query = @"query {
          projects {
            owner {
                managerId
            }
            settings {
              allowComments
            }
          }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = new List<Task> { new Task() },
                                Owner = new Person
                                {
                                    Id = 77,
                                    Manager = new Person { Id = 99 }
                                }
                            }
                        },
            };
            var serviceCollection = new ServiceCollection();
            var settings = new SettingsService();
            serviceCollection.AddSingleton(settings);

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic project = Enumerable.ElementAt((dynamic)res.Data["projects"], 0);
            Type projectType = project.GetType();
            Assert.Equal(1, settings.CallCount);
            Assert.Equal(2, projectType.GetFields().Count());
            Assert.Equal("owner", projectType.GetFields()[0].Name);
            Assert.Equal("managerId", project.owner.GetType().GetFields()[0].Name);
            Assert.Equal("settings", projectType.GetFields()[1].Name);
            Assert.Equal("allowComments", project.settings.GetType().GetFields()[0].Name);
        }

        [Fact]
        public void TestServiceThenSubSelectWithWhere()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("config",
                (p) => WithService((ConfigService srv) => srv.Get(p.Id)),
                "Get project config");
            // because of the selections of the config.type (a service) when we do our second selection (first is EF compatible)
            // the type of t will not match, need to be replaced
            schema.Type<Project>().ReplaceField("tasks", p => p.Tasks.Where(t => t.IsActive), "Active tasks");

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
          projects {
            config { type }
            tasks { id }
          }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = new List<Task>
                                {
                                    new Task
                                    {
                                        Id = 98
                                    }
                                }
                            }
                        },
            };

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic project = Enumerable.ElementAt((dynamic)res.Data["projects"], 0);
            Type projectType = project.GetType();
            Assert.Equal(2, projectType.GetFields().Count());
            Assert.Equal("config", projectType.GetFields()[0].Name);
            Assert.Equal("tasks", projectType.GetFields()[1].Name);
        }

        [Fact]
        public void TestServiceThenSubSelectWithWhereAlreadySelectedFieldInWhere()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("config",
                (p) => WithService((ConfigService srv) => srv.Get(p.Id)),
                "Get project config");
            // because of the selections of the config.type (a service) when we do our second selection (first is EF compatible)
            // the type of t will not match, need to be replaced
            schema.Type<Project>().ReplaceField("tasks", p => p.Tasks.Where(t => t.IsActive), "Active tasks");

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
          projects {
            config { type }
            tasks { id isActive }
          }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = new List<Task>
                                {
                                    new Task { Id = 98 }
                                }
                            }
                        },
            };

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic project = Enumerable.ElementAt((dynamic)res.Data["projects"], 0);
            Type projectType = project.GetType();
            Assert.Equal(2, projectType.GetFields().Count());
            Assert.Equal("config", projectType.GetFields()[0].Name);
            Assert.Equal("tasks", projectType.GetFields()[1].Name);
        }

        [Fact]
        public void TestServiceThenSubSelectWithWhereDuplicateFieldNames()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("config",
                (p) => WithService((ConfigService srv) => srv.Get(p.Id)),
                "Get project config");
            // because of the selections of the config.type (a service) when we do our second selection (first is EF compatible)
            // the type of t will not match, need to be replaced
            schema.Type<Project>().ReplaceField("tasks", p => p.Tasks.Where(t => t.IsActive), "Active tasks");

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
          projects {
            config { type }
            tasks { isActive project { isActive } }
          }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = new List<Task>
                                {
                                    new Task { Id = 98, IsActive = true }
                                }
                            }
                        },
            };
            context.Projects.First().Tasks.First().Project = context.Projects.First();

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic project = Enumerable.ElementAt((dynamic)res.Data["projects"], 0);
            Type projectType = project.GetType();
            Assert.Equal(2, projectType.GetFields().Count());
            Assert.Equal("config", projectType.GetFields()[0].Name);
            Assert.Equal("tasks", projectType.GetFields()[1].Name);
        }

        [Fact]
        public void TestServiceThenSubOrderBy()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("config",
                (p) => WithService((ConfigService srv) => srv.Get(p.Id)),
                "Get project config");
            // because of the selections of the config.type (a service) when we do our second selection (first is EF compatible)
            // the type of t will not match, need to be replaced
            schema.Type<Project>().ReplaceField("tasks", p => p.Tasks.OrderBy(t => t.IsActive), "Ordered tasks");

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
          projects {
            config { type }
            tasks { isActive }
          }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = new List<Task>
                                {
                                    new Task { Id = 98, IsActive = true }
                                }
                            }
                        },
            };
            context.Projects.First().Tasks.First().Project = context.Projects.First();

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic project = Enumerable.ElementAt((dynamic)res.Data["projects"], 0);
            Type projectType = project.GetType();
            Assert.Equal(2, projectType.GetFields().Count());
            Assert.Equal("config", projectType.GetFields()[0].Name);
            Assert.Equal("tasks", projectType.GetFields()[1].Name);
        }

        [Fact]
        public void TestWhereWhenWithServiceField()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.Type<Project>().AddField("configType",
                (p) => WithService((ConfigService srv) => srv.Get(p.Id).Type),
                "Get project config");

            schema.ReplaceField("projects",
                new
                {
                    search = (string)null,
                },
                (ctx, args) => ctx.Projects
                    .WhereWhen(p => p.Description.ToLower().Contains(args.search), !string.IsNullOrEmpty(args.search))
                    .OrderBy(p => p.Description),
                "List of projects");

            var serviceCollection = new ServiceCollection();
            ConfigService srv = new();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
            projects {
                configType
            }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = null,
                                Description = "Hello"
                            }
                        },
            };

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic project = Enumerable.First((dynamic)res.Data["projects"]);
            Type projectType = project.GetType();
            Assert.Single(projectType.GetFields());
            Assert.Equal("configType", projectType.GetFields()[0].Name);
        }

        [Fact]
        public void TestWhereWhenWithServiceFieldAndArgument()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.Type<Project>().AddField("configType",
                (p) => WithService((ConfigService srv) => srv.Get(0).Type),
                "Get project config");

            schema.ReplaceField("projects",
                new
                {
                    search = (string)null,
                },
                (ctx, args) => ctx.Projects
                    .WhereWhen(p => p.Description.ToLower().Contains(args.search), !string.IsNullOrEmpty(args.search))
                    .OrderBy(p => p.Description),
                "List of projects");

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
            project(id: 0) {
                configType
            }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = null,
                                Description = "Hello"
                            }
                        },
            };

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic project = (dynamic)res.Data["project"];
            Type projectType = project.GetType();
            Assert.Single(projectType.GetFields());
            Assert.Equal("configType", projectType.GetFields()[0].Name);
        }

        [Fact]
        public void TestCollectionToSingleWithServiceAndCollectionFragmentWithServiceAndSkippedRelation()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.Type<Project>().AddField("configType",
                (p) => WithService((ConfigService srv) => srv.Get(0).Type),
                "Get project config");

            schema.Type<Task>().AddField("assigneeProjects", t => t.Assignee.Projects, "All projects for assignee");
            schema.Type<Person>().AddField("age",
                // use a filed not another relation/entity
                (person) => WithService((AgeService ager) => ager.GetAge(person.Birthday)),
                "Persons age");

            var serviceCollection = new ServiceCollection();
            ConfigService configSrv = new ConfigService();
            serviceCollection.AddSingleton(configSrv);
            AgeService ageSrv = new AgeService();
            serviceCollection.AddSingleton(ageSrv);

            var gql = new QueryRequest
            {
                Query = @"
        fragment taskFrag on Task {
            assigneeProjects { # skips relation in context
                id
            }
            assignee { # relation
                age # service
            }
        }

        query {
            project(id: 0) { # context collection to single
                description
                configType # service field
                tasks {
                    ...taskFrag
                }
            }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Id = 0,
                                Description = "Hello",
                                Tasks = new List<Task>
                                {
                                    new Task
                                    {
                                        Assignee = new Person
                                        {
                                            Name = "Billy",
                                            Projects = new List<Project>()
                                        }
                                    }
                                },
                            }
                        },
            };

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, configSrv.CallCount);
            Assert.Equal(1, ageSrv.CallCount);
            dynamic project = (dynamic)res.Data["project"];
        }

        [Fact]
        public void TestCollectionToSingleWithServiceInCollection()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.Type<Project>().AddField("configType",
                (p) => WithService((ConfigService srv) => srv.Get(0).Type),
                "Get project config");

            var serviceCollection = new ServiceCollection();
            ConfigService configSrv = new ConfigService();
            serviceCollection.AddSingleton(configSrv);

            var gql = new QueryRequest
            {
                Query = @"query {
            task(id: 1) { # context collection to single
                project {
                    configType
                }
            }
        }"
            };

            var context = new TestDataContext
            {
                Tasks = new List<Task>
                        {
                            new Task
                            {
                                Id = 1,
                                Project = new Project
                                {
                                    Id = 0,
                                    Description = "Hello",
                                }
                            }
                        },
            };

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, configSrv.CallCount);
            dynamic project = (dynamic)res.Data["task"];
        }

        [Fact]
        public void TestCollectionToSingleWithServiceTypeInCollection()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<ProjectConfig>("ProjectConfig", "Config for the project").AddAllFields();
            schema.Type<Project>().AddField("config",
                (p) => WithService((ConfigService srv) => srv.Get(0)),
                "Get project config");

            var serviceCollection = new ServiceCollection();
            ConfigService configSrv = new ConfigService();
            serviceCollection.AddSingleton(configSrv);

            var gql = new QueryRequest
            {
                Query = @"query {
            task(id: 1) { # context collection to single
                project {
                    config {
                        type
                    }
                }
            }
        }"
            };

            var context = new TestDataContext
            {
                Tasks = new List<Task>
                        {
                            new Task
                            {
                                Id = 1,
                                Project = new Project
                                {
                                    Id = 0,
                                    Description = "Hello",
                                }
                            }
                        },
            };

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, configSrv.CallCount);
            dynamic project = (dynamic)res.Data["task"];
        }

        [Fact]
        public void TestCollectionToSingleWithServiceTypeInCollectionThatUsesAContextField()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<ProjectConfig>("ProjectConfig", "Config for the project").AddAllFields();
            schema.Type<Project>().AddField("config",
                (p) => WithService((ConfigService srv) => srv.Get(p.Id)),
                "Get project config");

            var serviceCollection = new ServiceCollection();
            ConfigService configSrv = new();
            serviceCollection.AddSingleton(configSrv);

            var gql = new QueryRequest
            {
                Query = @"query {
            task(id: 1) { # context collection to single
                project {
                    config {
                        type
                    }
                }
            }
        }"
            };

            var context = new TestDataContext
            {
                Tasks = new List<Task>
                        {
                            new Task
                            {
                                Id = 1,
                                Project = new Project
                                {
                                    Id = 0,
                                    Description = "Hello",
                                }
                            }
                        },
            };

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, configSrv.CallCount);
            dynamic project = (dynamic)res.Data["task"];
        }

        [Fact]
        public void TestServiceAfterMultipleCollectionToSingle()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<ProjectConfig>("ProjectConfig", "Config for the task").AddAllFields();
            schema.Type<Task>().AddField("config",
                (p) => WithService((ConfigService srv) => srv.Get(p.Id)),
                "Get task config");
            schema.Type<Project>().AddField("firstTask",
                (p) => p.Tasks.FirstOrDefault(),
                "Get first task");

            var serviceCollection = new ServiceCollection();
            ConfigService configSrv = new();
            serviceCollection.AddSingleton(configSrv);

            var gql = new QueryRequest
            {
                Query = @"query {
                    project(id: 0) { # context collection to single
                        firstTask { # still EF context but uses first()
                            config { #service
                                type
                            }
                        }
                    }
                }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                {
                    new Project
                    {
                        Id = 0,
                        Description = "Hello",
                        Tasks = new List<Task>
                        {
                            new Task
                            {
                                Id = 1,
                            }
                        },
                    }
                }
            };

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, configSrv.CallCount);
            dynamic project = (dynamic)res.Data["project"];
        }

        [Fact]
        public void TestCollectionCollectionObjectThenService()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.Type<Person>().AddField("age",
                (p) => WithService((AgeService srv) => srv.GetAge(p.Birthday)),
                "Get age");

            var serviceCollection = new ServiceCollection();
            AgeService service = new();
            serviceCollection.AddSingleton(service);

            var gql = new QueryRequest
            {
                Query = @"query {
                    projects {
                        tasks {
                            assignee {
                                age
                                __typename
                            }
                        }
                    }
                }"
            };

            var context = new TestDataContext();
            context.FillWithTestData();
            context.Projects.ElementAt(0).Tasks.ElementAt(0).Assignee = new Person
            {
                Birthday = new DateTime(1990, 9, 3),
                Name = "yo"
            };

            var res = schema.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, service.CallCount);
            dynamic projects = (dynamic)res.Data["projects"];
            Assert.Equal(2, projects[0].tasks[0].assignee.GetType().GetFields().Length);
        }

        public class AgeService
        {
            public AgeService()
            {
                CallCount = 0;
            }

            public int CallCount { get; private set; }

            public int GetAge(DateTime? birthday)
            {
                CallCount += 1;
                // you could do smarter things here like use other services
                return birthday.HasValue ? (int)(DateTime.Now - birthday.Value).TotalDays / 365 : 0;
            }
        }

        public class ConfigService
        {
            public ConfigService()
            {
                CallCount = 0;
            }

            public int CallCount { get; set; }

            public ProjectConfig Get(int id)
            {
                CallCount += 1;
                return new ProjectConfig
                {
                    Type = "Something"
                };
            }
        }

        public class EntityPager
        {
            public EntityPager()
            {
                CallCount = 0;
            }

            public int CallCount { get; private set; }
            public Pagination<Project> PageProjects(TestDataContext db, dynamic arg)
            {
                CallCount += 1;
                int page = (int)arg.page;
                int pagesize = (int)arg.pagesize;

                //Pagination
                int total = db.Projects.Count();
                int pagecount = total + pagesize / pagesize;
                int skipTo = (page * pagesize) - pagesize;

                //Data
                var projects = db.Projects
                    .Skip(skipTo)
                    .Take(pagesize);

                return new Pagination<Project> { Total = total, PageCount = pagecount, Items = projects };
            }
            public Pagination<Project> PageProjects(IEnumerable<Project> projects, dynamic arg)
            {
                CallCount += 1;
                int page = (int)arg.page;
                int pagesize = (int)arg.pagesize;

                //Pagination
                int total = projects.Count();
                int pagecount = total + pagesize / pagesize;
                int skipTo = (page * pagesize) - pagesize;

                //Data
                var newProjects = projects
                    .Skip(skipTo)
                    .Take(pagesize);

                return new Pagination<Project> { Total = total, PageCount = pagecount, Items = newProjects };
            }
        }


        public class Pagination<TEntity>
        {
            [GraphQLNotNull]
            public IEnumerable<TEntity> Items { get; set; }
            [GraphQLNotNull]
            public int Total { get; set; }
            [GraphQLNotNull]

            public int PageCount { get; set; }
        }
    }

    public class SettingsService
    {
        public int CallCount { get; internal set; }

        public Settings Get(int id, bool someBool)
        {
            CallCount += 1;
            return new Settings();
        }
    }

    public class Settings
    {
        public bool AllowComments { get; set; } = false;
    }

    internal class PagerArgs
    {
        public int page { get; set; }
        public int pagesize { get; set; }
    }

    public class UserService
    {
        public UserService()
        {
            CallCount = 0;
        }

        public int CallCount { get; private set; }
        public User GetUser()
        {
            CallCount += 1;
            return new User();
        }
        public IEnumerable<User> GetUsers()
        {
            CallCount += 1;
            return new List<User> { new User() };
        }
    }

    public class ProjectConfig
    {
        public string Type { get; set; }
    }
}