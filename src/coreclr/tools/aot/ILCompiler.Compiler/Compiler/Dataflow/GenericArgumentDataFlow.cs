// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using ILCompiler.DependencyAnalysis;
using ILCompiler.Logging;

using ILLink.Shared.TrimAnalysis;

using Internal.TypeSystem;

using DependencyList = ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.DependencyList;
using MultiValue = ILLink.Shared.DataFlow.ValueSet<ILLink.Shared.DataFlow.SingleValue>;

#nullable enable

namespace ILCompiler.Dataflow
{
    public static class GenericArgumentDataFlow
    {
        public static void ProcessGenericArgumentDataFlow(ref DependencyList dependencies, Logger logger, NodeFactory factory, FlowAnnotations annotations, in MessageOrigin origin, TypeDesc type)
        {
            var diagnosticContext = new DiagnosticContext(
                origin,
                !logger.ShouldSuppressAnalysisWarningsForRequires(origin.MemberDefinition, DiagnosticUtilities.RequiresUnreferencedCodeAttribute),
                logger);
            var reflectionMarker = new ReflectionMarker(logger, factory, annotations, typeHierarchyDataFlowOrigin: null, enabled: true);

            ProcessGenericArgumentDataFlow(diagnosticContext, reflectionMarker, type);

            if (reflectionMarker.Dependencies.Count > 0)
            {
                if (dependencies == null)
                    dependencies = reflectionMarker.Dependencies;
                else
                    dependencies.AddRange(reflectionMarker.Dependencies);
            }
        }

        public static void ProcessGenericArgumentDataFlow(in DiagnosticContext diagnosticContext, ReflectionMarker reflectionMarker, TypeDesc type)
        {
            TypeDesc typeDefinition = type.GetTypeDefinition();
            if (typeDefinition != type)
            {
                ProcessGenericInstantiation(diagnosticContext, reflectionMarker, type.Instantiation, typeDefinition.Instantiation);
            }
        }

        public static void ProcessGenericArgumentDataFlow(in DiagnosticContext diagnosticContext, ReflectionMarker reflectionMarker, MethodDesc method)
        {
            MethodDesc typicalMethod = method.GetTypicalMethodDefinition();
            if (typicalMethod != method)
            {
                ProcessGenericInstantiation(diagnosticContext, reflectionMarker, method.Instantiation, typicalMethod.Instantiation);
            }

            ProcessGenericArgumentDataFlow(diagnosticContext, reflectionMarker, method.OwningType);
        }

        public static void ProcessGenericArgumentDataFlow(in DiagnosticContext diagnosticContext, ReflectionMarker reflectionMarker, FieldDesc field)
        {
            ProcessGenericArgumentDataFlow(diagnosticContext, reflectionMarker, field.OwningType);
        }

        private static void ProcessGenericInstantiation(in DiagnosticContext diagnosticContext, ReflectionMarker reflectionMarker, Instantiation instantiation, Instantiation typicalInstantiation)
        {
            for (int i = 0; i < instantiation.Length; i++)
            {
                var genericParameter = (GenericParameterDesc)typicalInstantiation[i];
                if (reflectionMarker.Annotations.GetGenericParameterAnnotation(genericParameter) != default)
                {
                    var genericParameterValue = reflectionMarker.Annotations.GetGenericParameterValue(genericParameter);
                    Debug.Assert(genericParameterValue.DynamicallyAccessedMemberTypes != DynamicallyAccessedMemberTypes.None);
                    MultiValue genericArgumentValue = reflectionMarker.Annotations.GetTypeValueFromGenericArgument(instantiation[i]);
                    var requireDynamicallyAccessedMembersAction = new RequireDynamicallyAccessedMembersAction(reflectionMarker, diagnosticContext, genericParameter.GetDisplayName());
                    requireDynamicallyAccessedMembersAction.Invoke(genericArgumentValue, genericParameterValue);
                }
            }
        }
    }
}
