﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.OpenApiSpecification.Core.Models;
using Microsoft.OpenApiSpecification.Generation.Extensions;
using Microsoft.OpenApiSpecification.Generation.Models;

namespace Microsoft.OpenApiSpecification.Generation.OperationFilters
{
    /// <summary>
    /// Parses the value of param tag in xml documentation and apply that as request body in operation.
    /// </summary>
    public class ApplyParamAsRequestBodyFilter : IOperationFilter
    {
        /// <summary>
        /// Fetches the value of "param" tags from xml documentation with in valus of "body"
        /// and populates operation's request body.
        /// </summary>
        /// <param name="operation">The operation to be updated.</param>
        /// <param name="element">The xml element representing an operation in the annotation xml.</param>
        /// <param name="settings">The operation filter settings.</param>
        /// <remarks>
        /// Care should be taken to not overwrite the existing value in Operation if already present.
        /// This guarantees the predictable behavior that the first tag in the XML will be respected.
        /// It also guarantees that common annotations in the config file do not overwrite the
        /// annotations in the main documentation.
        /// </remarks>
        public void Apply(Operation operation, XElement element, OperationFilterSettings settings)
        {
            var bodyElements = element.Elements()
                .Where(p => p.Name == KnownStrings.Param && p.Attribute(KnownStrings.In)?.Value == KnownStrings.Body)
                .ToList();

            foreach (var bodyElement in bodyElements)
            {
                var childNodes = bodyElement.DescendantNodes().ToList();
                var description = string.Empty;

                var lastNode = childNodes.LastOrDefault();

                if (lastNode != null && lastNode.NodeType == XmlNodeType.Text)
                {
                    description = lastNode.ToString();
                }

                var allListedTypes = new List<string>();

                var seeNodes = bodyElement.Descendants("see");

                foreach (var node in seeNodes)
                {
                    var crefValue = node.Attribute(KnownStrings.Cref)?.Value;

                    if (crefValue != null)
                    {
                        allListedTypes.Add(crefValue);
                    }
                }

                var type = settings.TypeFetcher.GetTypeFromCrefValues(allListedTypes);

                var schema = settings.ReferenceRegistryManager.SchemaReferenceRegistry.FindOrAddReference(type);

                if (operation.RequestBody == null)
                {
                    operation.RequestBody = new RequestBody
                    {
                        Description = description.RemoveBlankLines(),
                        Content =
                        {
                            ["application/json"] = new MediaType {Schema = schema}
                        },
                        IsRequired = true
                    };
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(operation.RequestBody.Description))
                    {
                        operation.RequestBody.Description = description.RemoveBlankLines();
                    }

                    if (!operation.RequestBody.Content["application/json"].Schema.AnyOf.Any())
                    {
                        var existingSchema = operation.RequestBody.Content["application/json"].Schema;
                        var newSchema = new Schema();
                        newSchema.AnyOf.Add(existingSchema);

                        operation.RequestBody.Content["application/json"].Schema = newSchema;
                    }

                    operation.RequestBody.Content["application/json"].Schema.AnyOf.Add(schema);
                }
            }
        }
    }
}