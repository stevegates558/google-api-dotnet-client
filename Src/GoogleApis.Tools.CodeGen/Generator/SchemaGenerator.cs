/*
Copyright 2010 Google Inc

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System.CodeDom;
using System.Collections.Generic;
using log4net;
using Newtonsoft.Json.Schema;
using Google.Apis.Discovery.Schema;
using Google.Apis.Tools.CodeGen.Decorator.SchemaDecorator;
using Google.Apis.Util;
using Google.Apis.Testing;

namespace Google.Apis.Tools.CodeGen.Generator
{
    /// <summary>
    /// Schema Generator
    /// </summary>
    public class SchemaGenerator : BaseGenerator
    {
        private readonly IEnumerable<ISchemaDecorator> decorators;

        public SchemaGenerator(IEnumerable<ISchemaDecorator> decorators)
        {
            decorators.ThrowIfNull("decorators");
            this.decorators = decorators;
        }

        /// <summary>
        /// Creates a fully working class for the specified schema
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="otherSchemaNames"></param>
        /// <returns></returns>
        public CodeTypeDeclaration CreateClass(ISchema schema, IEnumerable<string> otherSchemaNames)
        {
            schema.ThrowIfNull("schema");
            otherSchemaNames.ThrowIfNull("otherSchmeaNames");

            string className = GeneratorUtils.GetClassName(schema, otherSchemaNames);
            var typeDeclaration = new CodeTypeDeclaration(className);
            var nestedClassGenerator = new NestedClassGenerator(typeDeclaration, decorators, "");
            foreach (ISchemaDecorator schemaDecorator in decorators)
            {
                schemaDecorator.DecorateClass(typeDeclaration, schema, nestedClassGenerator);
            }
            nestedClassGenerator.GenerateNestedClasses();

            return typeDeclaration;
        }

        #region Nested type: NestedClassGenerator

        [VisibleForTestOnly]
        internal class NestedClassGenerator : INestedClassProvider
        {
            private static readonly ILog logger = LogManager.GetLogger(typeof(NestedClassGenerator));

            private readonly IEnumerable<ISchemaDecorator> decorators;

            /// <summary>
            /// Maps Schemas to the name they received so schemas found multiple time will resolve to the same name.
            /// This also allows us to generate the internal classes at the end instead of as we find them.
            /// </summary>
            private readonly IDictionary<JsonSchema, string> knownSubschemas;

            private readonly CodeTypeDeclaration typeDeclaration;

            /// <summary>A string to make this nested class names unique</summary>
            private readonly string uniquefier;

            private readonly NestedClassNameGenerator nameGenerator;

            public NestedClassGenerator(CodeTypeDeclaration typeDeclaration,
                                        IEnumerable<ISchemaDecorator> decorators,
                                        string uniquefier)
            {
                this.typeDeclaration = typeDeclaration;
                this.decorators = decorators;
                knownSubschemas = new Dictionary<JsonSchema, string>();
                nameGenerator = new NestedClassNameGenerator();
                this.uniquefier = uniquefier;
            }

            #region INestedClassProvider Members

            /// <summary>
            /// Gets a class name as a CodeTypeReference for the given schema of the form "IntenalClassN" where 
            /// N is an integer. Given the same JsonSchema this will return the same classname.
            /// </summary>
            public CodeTypeReference GetClassName(JsonSchema definition)
            {
                if (knownSubschemas.ContainsKey(definition))
                {
                    return new CodeTypeReference(knownSubschemas[definition]);
                }

                string name = null;
                
                // First, try to generate a name based upon the environment
                if (typeDeclaration != null)
                {
                    var generatedName = nameGenerator.GenerateName(typeDeclaration, definition);

                    if (generatedName.IsNotNullOrEmpty() &&
                        knownSubschemas.Values.Contains(generatedName) == false &&
                        typeDeclaration.Members.FindTypeMemberByName(generatedName) == null &&
                        typeDeclaration.Name != generatedName)
                    {
                        // The generated name is not taken, and differs from the parent type -> use it
                        name = generatedName;
                    }
                }

                // If this name collides with an existing type, generate a unique name)
                if (name == null)
                {
                    name = GetSchemaName(knownSubschemas.Count+1);
                }

                knownSubschemas.Add(definition,name);
                return new CodeTypeReference(name);
            }

            #endregion

            public void GenerateNestedClasses()
            {
                int i = 0;
                foreach (var pair in knownSubschemas)
                {
                    typeDeclaration.Members.Add(GenerateNestedClass(pair.Key, i+1));
                }
            }

            [VisibleForTestOnly]
            internal CodeTypeDeclaration GenerateNestedClass(JsonSchema schema, int orderOfNestedClass)
            {
                schema.ThrowIfNull("schema");
                string className = GetClassName(schema).BaseType;
                var typeDeclaration = new CodeTypeDeclaration(className);
                typeDeclaration.Attributes = MemberAttributes.Public;
                var nestedClassGenerator = new NestedClassGenerator(
                    typeDeclaration, decorators, uniquefier + orderOfNestedClass + "_");
                foreach (ISchemaDecorator schemaDecorator in decorators)
                {
                    if (schemaDecorator is INestedClassSchemaDecorator)
                    {
                        logger.DebugFormat(
                            "Found IInternalClassSchemaDecorator {0} - decorating {1}", schemaDecorator, className);
                        ((INestedClassSchemaDecorator) schemaDecorator).DecorateInternalClass(
                            typeDeclaration, className, schema, nestedClassGenerator);
                    }
                }
                nestedClassGenerator.GenerateNestedClasses();

                return typeDeclaration;
            }

            private string GetSchemaName(int schemaNumber)
            {
                return string.Format("NestedClass{0}{1}", uniquefier, schemaNumber);
            }
        }

        #endregion
    }
}