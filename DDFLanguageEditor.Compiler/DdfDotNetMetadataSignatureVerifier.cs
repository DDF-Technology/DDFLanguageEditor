using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace DDFLanguageEditor.Compiler
{
    internal readonly struct DdfDotNetSignatureType
    {
        internal DdfDotNetSignatureType(string display, bool unsafeType = false)
        {
            Display = display ?? string.Empty;
            UnsafeType = unsafeType;
        }

        internal string Display { get; }
        internal bool UnsafeType { get; }
        public override string ToString() => Display;
    }

    internal sealed class DdfDotNetSignatureTypeProvider :
        ISignatureTypeProvider<DdfDotNetSignatureType, object>
    {
        internal static DdfDotNetSignatureTypeProvider Instance { get; } = new();

        public DdfDotNetSignatureType GetArrayType(DdfDotNetSignatureType elementType,
            ArrayShape shape) => new(elementType + "[" + shape.Rank + "]",
            elementType.UnsafeType);

        public DdfDotNetSignatureType GetByReferenceType(
            DdfDotNetSignatureType elementType) => new("ref " + elementType,
            elementType.UnsafeType);

        public DdfDotNetSignatureType GetFunctionPointerType(
            MethodSignature<DdfDotNetSignatureType> signature) =>
            new("function-pointer", unsafeType: true);

        public DdfDotNetSignatureType GetGenericInstantiation(
            DdfDotNetSignatureType genericType,
            ImmutableArray<DdfDotNetSignatureType> typeArguments) =>
            new(genericType + "<" + string.Join(",", typeArguments) + ">",
                genericType.UnsafeType || typeArguments.Any(item =>
                    item.UnsafeType));

        public DdfDotNetSignatureType GetGenericMethodParameter(object context,
            int index) => new("!!" + index);

        public DdfDotNetSignatureType GetGenericTypeParameter(object context,
            int index) => new("!" + index);

        public DdfDotNetSignatureType GetModifiedType(
            DdfDotNetSignatureType modifier,
            DdfDotNetSignatureType unmodifiedType, bool isRequired) =>
            new((isRequired ? "modreq(" : "modopt(") + modifier + ") " +
                unmodifiedType, modifier.UnsafeType ||
                unmodifiedType.UnsafeType);

        public DdfDotNetSignatureType GetPinnedType(
            DdfDotNetSignatureType elementType) => new("pinned " + elementType,
            elementType.UnsafeType);

        public DdfDotNetSignatureType GetPointerType(
            DdfDotNetSignatureType elementType) => new(elementType + "*",
            unsafeType: true);

        public DdfDotNetSignatureType GetPrimitiveType(PrimitiveTypeCode typeCode) =>
            new(typeCode.ToString(), typeCode == PrimitiveTypeCode.TypedReference);

        public DdfDotNetSignatureType GetSZArrayType(
            DdfDotNetSignatureType elementType) => new(elementType + "[]",
            elementType.UnsafeType);

        public DdfDotNetSignatureType GetTypeFromDefinition(MetadataReader reader,
            TypeDefinitionHandle handle, byte rawTypeKind) => new(
                TypeName(reader, reader.GetTypeDefinition(handle)));

        public DdfDotNetSignatureType GetTypeFromReference(MetadataReader reader,
            TypeReferenceHandle handle, byte rawTypeKind) => new(
                TypeName(reader, reader.GetTypeReference(handle)));

        public DdfDotNetSignatureType GetTypeFromSpecification(
            MetadataReader reader, object context, TypeSpecificationHandle handle,
            byte rawTypeKind) => reader.GetTypeSpecification(handle)
            .DecodeSignature(this, context);

        private static string TypeName(MetadataReader reader,
            TypeDefinition type) => Join(reader.GetString(type.Namespace),
                reader.GetString(type.Name));

        private static string TypeName(MetadataReader reader,
            TypeReference type) => Join(reader.GetString(type.Namespace),
                reader.GetString(type.Name));

        private static string Join(string prefix, string name) =>
            string.IsNullOrEmpty(prefix) ? name : prefix + "." + name;
    }

    internal static class DdfDotNetMetadataSignatureVerifier
    {
        internal static void Verify(string moduleIdentity,
            MetadataReader metadata,
            ICollection<DdfDotNetContractIssue> issues)
        {
            var unsafeSignatures = new List<string>();
            try
            {
                foreach (FieldDefinitionHandle handle in metadata.FieldDefinitions)
                    Check(metadata.GetFieldDefinition(handle).DecodeSignature(
                        DdfDotNetSignatureTypeProvider.Instance, null), "field");
                foreach (MethodDefinitionHandle handle in metadata.MethodDefinitions)
                    CheckMethod(metadata.GetMethodDefinition(handle).DecodeSignature(
                        DdfDotNetSignatureTypeProvider.Instance, null), "method");
                foreach (PropertyDefinitionHandle handle in metadata
                             .PropertyDefinitions)
                    CheckMethod(metadata.GetPropertyDefinition(handle).DecodeSignature(
                        DdfDotNetSignatureTypeProvider.Instance, null), "property");
                foreach (TypeSpecificationHandle handle in Handles(
                             metadata.GetTableRowCount(TableIndex.TypeSpec),
                             MetadataTokens.TypeSpecificationHandle))
                    Check(metadata.GetTypeSpecification(handle).DecodeSignature(
                        DdfDotNetSignatureTypeProvider.Instance, null), "type");
                foreach (MethodSpecificationHandle handle in Handles(
                             metadata.GetTableRowCount(TableIndex.MethodSpec),
                             MetadataTokens.MethodSpecificationHandle))
                    foreach (DdfDotNetSignatureType type in metadata
                                 .GetMethodSpecification(handle).DecodeSignature(
                                     DdfDotNetSignatureTypeProvider.Instance,
                                     null))
                        Check(type, "method-specification");
                foreach (MemberReferenceHandle handle in metadata.MemberReferences)
                {
                    MemberReference reference = metadata.GetMemberReference(handle);
                    if (reference.GetKind() == MemberReferenceKind.Method)
                        CheckMethod(reference.DecodeMethodSignature(
                            DdfDotNetSignatureTypeProvider.Instance, null),
                            "member-method");
                    else
                        Check(reference.DecodeFieldSignature(
                            DdfDotNetSignatureTypeProvider.Instance, null),
                            "member-field");
                }
                foreach (StandaloneSignatureHandle handle in Handles(
                             metadata.GetTableRowCount(TableIndex.StandAloneSig),
                             MetadataTokens.StandaloneSignatureHandle))
                {
                    StandaloneSignature signature = metadata
                        .GetStandaloneSignature(handle);
                    if (signature.GetKind() ==
                        StandaloneSignatureKind.LocalVariables)
                        foreach (DdfDotNetSignatureType type in signature
                                     .DecodeLocalSignature(
                                         DdfDotNetSignatureTypeProvider.Instance,
                                         null))
                            Check(type, "local");
                    else
                        CheckMethod(signature.DecodeMethodSignature(
                            DdfDotNetSignatureTypeProvider.Instance, null),
                            "standalone-method");
                }
            }
            catch (Exception exception) when (exception is BadImageFormatException ||
                                               exception is InvalidOperationException ||
                                               exception is ArgumentOutOfRangeException)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC517",
                    moduleIdentity + ": firma metadati non valida: " +
                    exception.Message));
                return;
            }
            if (unsafeSignatures.Count != 0)
                issues.Add(new DdfDotNetContractIssue("DDFC517",
                    moduleIdentity +
                    ": firma fuori dal profilo DDF sicuro: " +
                    unsafeSignatures[0] + "."));

            void Check(DdfDotNetSignatureType type, string owner)
            {
                if (type.UnsafeType) unsafeSignatures.Add(owner + " " + type);
            }

            void CheckMethod(MethodSignature<DdfDotNetSignatureType> signature,
                string owner)
            {
                Check(signature.ReturnType, owner + " return");
                foreach (DdfDotNetSignatureType type in signature.ParameterTypes)
                    Check(type, owner + " parameter");
            }
        }

        private static IEnumerable<THandle> Handles<THandle>(int count,
            Func<int, THandle> factory)
        {
            for (int row = 1; row <= count; row++) yield return factory(row);
        }
    }
}
