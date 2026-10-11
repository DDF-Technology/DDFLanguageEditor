using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetPeVerificationLimits
    {
        public DdfDotNetPeVerificationLimits(int maxTypes = 50000,
            int maxMethods = 100000, int maxFields = 200000,
            int maxMethodBodyBytes = 1024 * 1024,
            long maxTotalIlBytes = 64L * 1024 * 1024,
            long maxInstructions = 5000000,
            int maxExceptionRegions = 100000,
            int maxSwitchTargetsPerInstruction = 100000)
        {
            if (maxTypes <= 0 || maxMethods <= 0 || maxFields <= 0 ||
                maxMethodBodyBytes <= 0 || maxTotalIlBytes <= 0 ||
                maxInstructions <= 0 || maxExceptionRegions < 0 ||
                maxSwitchTargetsPerInstruction <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxTypes));
            MaxTypes = maxTypes;
            MaxMethods = maxMethods;
            MaxFields = maxFields;
            MaxMethodBodyBytes = maxMethodBodyBytes;
            MaxTotalIlBytes = maxTotalIlBytes;
            MaxInstructions = maxInstructions;
            MaxExceptionRegions = maxExceptionRegions;
            MaxSwitchTargetsPerInstruction = maxSwitchTargetsPerInstruction;
        }

        public static DdfDotNetPeVerificationLimits Default { get; } = new();
        public int MaxTypes { get; }
        public int MaxMethods { get; }
        public int MaxFields { get; }
        public int MaxMethodBodyBytes { get; }
        public long MaxTotalIlBytes { get; }
        public long MaxInstructions { get; }
        public int MaxExceptionRegions { get; }
        public int MaxSwitchTargetsPerInstruction { get; }
    }

    internal sealed class DdfDotNetIlVerificationSummary
    {
        internal long TypeDefinitions { get; set; }
        internal long MethodDefinitions { get; set; }
        internal long FieldDefinitions { get; set; }
        internal long MethodBodies { get; set; }
        internal long Instructions { get; set; }
        internal long TotalIlBytes { get; set; }
        internal long ExceptionRegions { get; set; }
    }

    internal static class DdfDotNetIlVerifier
    {
        private sealed class Instruction
        {
            internal int Offset { get; init; }
            internal int EndOffset { get; init; }
            internal OpCode OpCode { get; init; }
            internal int? Token { get; init; }
            internal int? VariableIndex { get; init; }
            internal IReadOnlyList<int> BranchTargets { get; init; } =
                Array.Empty<int>();
        }

        private readonly struct MethodStackSignature
        {
            internal MethodStackSignature(int parameters, bool hasThis,
                bool returnsValue)
            {
                Parameters = parameters;
                HasThis = hasThis;
                ReturnsValue = returnsValue;
            }

            internal int Parameters { get; }
            internal bool HasThis { get; }
            internal bool ReturnsValue { get; }
        }

        private static readonly IReadOnlyDictionary<byte, OpCode> OneByte =
            CreateOpCodes(twoByte: false);
        private static readonly IReadOnlyDictionary<byte, OpCode> TwoByte =
            CreateOpCodes(twoByte: true);

        internal static DdfDotNetIlVerificationSummary Verify(
            string moduleIdentity, PEReader pe, MetadataReader metadata,
            DdfDotNetPeVerificationLimits limits,
            ICollection<DdfDotNetContractIssue> issues)
        {
            var summary = new DdfDotNetIlVerificationSummary();
            summary.TypeDefinitions = metadata.TypeDefinitions.Count;
            summary.MethodDefinitions = metadata.MethodDefinitions.Count;
            summary.FieldDefinitions = metadata.FieldDefinitions.Count;
            if (metadata.TypeDefinitions.Count > limits.MaxTypes ||
                metadata.MethodDefinitions.Count > limits.MaxMethods ||
                metadata.FieldDefinitions.Count > limits.MaxFields)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC511",
                    moduleIdentity +
                    ": le tabelle metadati superano i limiti strutturali."));
                return summary;
            }
            DdfDotNetMetadataSignatureVerifier.Verify(moduleIdentity, metadata,
                issues);
            if (issues.Any(issue => issue.Code == "DDFC517")) return summary;
            int exceptionRegions = 0;
            foreach (MethodDefinitionHandle handle in metadata.MethodDefinitions)
            {
                MethodDefinition method = metadata.GetMethodDefinition(handle);
                if (method.RelativeVirtualAddress == 0) continue;
                try
                {
                    MethodBodyBlock body = pe.GetMethodBody(
                        method.RelativeVirtualAddress);
                    byte[] il = body.GetILBytes().ToArray();
                    if (il.Length > limits.MaxMethodBodyBytes ||
                        summary.TotalIlBytes > limits.MaxTotalIlBytes - il.Length)
                    {
                        issues.Add(new DdfDotNetContractIssue("DDFC511",
                            moduleIdentity +
                            ": un corpo IL o il totale IL supera il budget."));
                        return summary;
                    }
                    summary.MethodBodies++;
                    summary.TotalIlBytes += il.Length;
                    exceptionRegions += body.ExceptionRegions.Length;
                    summary.ExceptionRegions = exceptionRegions;
                    if (exceptionRegions > limits.MaxExceptionRegions)
                    {
                        issues.Add(new DdfDotNetContractIssue("DDFC511",
                            moduleIdentity +
                            ": troppe regioni di gestione eccezioni."));
                        return summary;
                    }
                    VerifyBody(moduleIdentity, metadata, method, body, il,
                        limits, summary, issues);
                    if (issues.Any(issue => issue.Code is "DDFC511" or
                            "DDFC512")) return summary;
                }
                catch (Exception exception) when (
                    exception is BadImageFormatException ||
                    exception is InvalidOperationException ||
                    exception is ArgumentOutOfRangeException)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC512",
                        moduleIdentity + ": corpo IL non leggibile: " +
                        exception.Message));
                    return summary;
                }
            }
            return summary;
        }

        private static void VerifyBody(string moduleIdentity,
            MetadataReader metadata, MethodDefinition method,
            MethodBodyBlock body, byte[] il,
            DdfDotNetPeVerificationLimits limits,
            DdfDotNetIlVerificationSummary summary,
            ICollection<DdfDotNetContractIssue> issues)
        {
            var instructions = new List<Instruction>();
            int position = 0;
            while (position < il.Length)
            {
                int offset = position;
                OpCode opCode = ReadOpCode(il, ref position);
                summary.Instructions++;
                if (summary.Instructions > limits.MaxInstructions)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC511",
                        moduleIdentity + ": il numero di istruzioni supera il budget."));
                    return;
                }
                if (opCode == OpCodes.Calli || opCode == OpCodes.Jmp ||
                    opCode == OpCodes.Localloc || opCode == OpCodes.Cpblk ||
                    opCode == OpCodes.Initblk)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC512",
                        moduleIdentity + ": opcode IL non consentito: " +
                        opCode.Name + "."));
                    return;
                }
                var branchTargets = new List<int>();
                int? token = ReadOperand(moduleIdentity, metadata,
                    opCode.OperandType, il, ref position, branchTargets,
                    limits, issues, out int? variableIndex);
                if (issues.Count != 0) return;
                instructions.Add(new Instruction
                {
                    Offset = offset,
                    EndOffset = position,
                    OpCode = opCode,
                    Token = token,
                    VariableIndex = variableIndex,
                    BranchTargets = branchTargets.AsReadOnly()
                });
            }
            var instructionOffsets = instructions.Select(item => item.Offset)
                .ToHashSet();
            foreach (int target in instructions.SelectMany(item =>
                         item.BranchTargets))
                if (!instructionOffsets.Contains(target))
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC512",
                        moduleIdentity +
                        ": destinazione di branch non allineata a un'istruzione."));
                    return;
                }
            if (!body.LocalSignature.IsNil)
                ValidateToken(moduleIdentity, metadata,
                    MetadataTokens.GetToken(body.LocalSignature),
                    OperandType.InlineSig, issues);
            foreach (ExceptionRegion region in body.ExceptionRegions)
            {
                if (!ValidRange(region.TryOffset, region.TryLength, il.Length,
                        instructionOffsets) ||
                    !ValidRange(region.HandlerOffset, region.HandlerLength,
                        il.Length, instructionOffsets) ||
                    region.Kind == ExceptionRegionKind.Filter &&
                    !instructionOffsets.Contains(region.FilterOffset))
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC512",
                        moduleIdentity +
                        ": regione di gestione eccezioni fuori dal corpo IL."));
                    return;
                }
                if (region.Kind == ExceptionRegionKind.Catch)
                    ValidateToken(moduleIdentity, metadata,
                        MetadataTokens.GetToken(region.CatchType),
                        OperandType.InlineType, issues);
                if (issues.Count != 0) return;
            }
            VerifyStackDataflow(moduleIdentity, metadata, method, body,
                instructions, issues);
        }

        private static int? ReadOperand(string moduleIdentity,
            MetadataReader metadata, OperandType operandType, byte[] il,
            ref int position, ICollection<int> branchTargets,
            DdfDotNetPeVerificationLimits limits,
            ICollection<DdfDotNetContractIssue> issues,
            out int? variableIndex)
        {
            variableIndex = null;
            switch (operandType)
            {
                case OperandType.InlineNone:
                    return null;
                case OperandType.ShortInlineBrTarget:
                    Ensure(il, position, 1);
                    branchTargets.Add(position + 1 + unchecked((sbyte)il[position]));
                    position++;
                    return null;
                case OperandType.InlineBrTarget:
                    Ensure(il, position, 4);
                    int relative = BinaryPrimitives.ReadInt32LittleEndian(
                        il.AsSpan(position, 4));
                    position += 4;
                    branchTargets.Add(position + relative);
                    return null;
                case OperandType.InlineSwitch:
                    Ensure(il, position, 4);
                    int count = BinaryPrimitives.ReadInt32LittleEndian(
                        il.AsSpan(position, 4));
                    position += 4;
                    if (count < 0 || count > limits.MaxSwitchTargetsPerInstruction ||
                        count > (il.Length - position) / 4)
                    {
                        issues.Add(new DdfDotNetContractIssue("DDFC511",
                            moduleIdentity + ": tabella switch oltre il budget."));
                        return null;
                    }
                    int switchBase = position + count * 4;
                    for (int index = 0; index < count; index++)
                    {
                        int delta = BinaryPrimitives.ReadInt32LittleEndian(
                            il.AsSpan(position, 4));
                        position += 4;
                        branchTargets.Add(switchBase + delta);
                    }
                    return null;
                case OperandType.ShortInlineI:
                    Ensure(il, position, 1);
                    position++;
                    return null;
                case OperandType.ShortInlineVar:
                    Ensure(il, position, 1);
                    variableIndex = il[position];
                    position++;
                    return null;
                case OperandType.InlineVar:
                    Ensure(il, position, 2);
                    variableIndex = BinaryPrimitives.ReadUInt16LittleEndian(
                        il.AsSpan(position, 2));
                    position += 2;
                    return null;
                case OperandType.InlineI:
                case OperandType.ShortInlineR:
                    Ensure(il, position, 4);
                    position += 4;
                    return null;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    Ensure(il, position, 8);
                    position += 8;
                    return null;
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    Ensure(il, position, 4);
                    int token = BinaryPrimitives.ReadInt32LittleEndian(
                        il.AsSpan(position, 4));
                    position += 4;
                    ValidateToken(moduleIdentity, metadata, token, operandType,
                        issues);
                    return token;
                default:
                    issues.Add(new DdfDotNetContractIssue("DDFC512",
                        moduleIdentity + ": tipo operando IL non supportato: " +
                        operandType + "."));
                    return null;
            }
        }

        private static void VerifyStackDataflow(string moduleIdentity,
            MetadataReader metadata, MethodDefinition method,
            MethodBodyBlock body, IReadOnlyList<Instruction> instructions,
            ICollection<DdfDotNetContractIssue> issues)
        {
            if (instructions.Count == 0) return;
            var byOffset = instructions.ToDictionary(item => item.Offset);
            var depths = new Dictionary<int, int>();
            var pending = new Queue<int>();
            Seed(0, 0);
            foreach (ExceptionRegion region in body.ExceptionRegions)
            {
                Seed(region.HandlerOffset,
                    region.Kind is ExceptionRegionKind.Catch or
                        ExceptionRegionKind.Filter ? 1 : 0);
                if (region.Kind == ExceptionRegionKind.Filter)
                    Seed(region.FilterOffset, 1);
            }

            MethodStackSignature enclosing = DecodeSignature(metadata,
                method.Signature);
            int argumentCount = enclosing.Parameters +
                (enclosing.HasThis ? 1 : 0);
            int localCount = body.LocalSignature.IsNil ? 0 : metadata
                .GetStandaloneSignature(body.LocalSignature)
                .DecodeLocalSignature(DdfDotNetSignatureTypeProvider.Instance,
                    null).Length;
            foreach (Instruction instruction in instructions)
                if (!ValidVariableOperand(instruction, argumentCount,
                        localCount))
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC517",
                        moduleIdentity +
                        ": indice argomento/locale non valido all'offset " +
                        instruction.Offset + "."));
                    return;
                }
            while (pending.Count != 0 && issues.Count == 0)
            {
                int offset = pending.Dequeue();
                Instruction instruction = byOffset[offset];
                int input = depths[offset];
                (int pop, int push) = StackDelta(metadata, instruction,
                    enclosing);
                if (input < pop)
                {
                    Fail("stack IL insufficiente all'offset " + offset + ".");
                    return;
                }
                if ((instruction.OpCode == OpCodes.Leave ||
                     instruction.OpCode == OpCodes.Leave_S) && input != 0)
                {
                    Fail("leave richiede uno stack vuoto all'offset " +
                        offset + ".");
                    return;
                }
                int output = input - pop + push;
                if (output > body.MaxStack)
                {
                    Fail("stack IL oltre MaxStack all'offset " + offset + ".");
                    return;
                }
                if (instruction.OpCode == OpCodes.Ret && output != 0)
                {
                    Fail("ret lascia valori residui sullo stack all'offset " +
                        offset + ".");
                    return;
                }
                foreach (int successor in Successors(instruction,
                             instructions))
                    Seed(successor, output);
            }

            void Seed(int offset, int depth)
            {
                if (!byOffset.ContainsKey(offset))
                {
                    Fail("flusso IL verso un offset non valido: " + offset +
                        ".");
                    return;
                }
                if (depths.TryGetValue(offset, out int existing))
                {
                    if (existing != depth)
                        Fail("altezze stack incompatibili confluiscono all'offset " +
                            offset + ".");
                    return;
                }
                depths.Add(offset, depth);
                pending.Enqueue(offset);
            }

            void Fail(string message) => issues.Add(
                new DdfDotNetContractIssue("DDFC513",
                    moduleIdentity + ": " + message));
        }

        private static bool ValidVariableOperand(Instruction instruction,
            int argumentCount, int localCount)
        {
            OpCode opCode = instruction.OpCode;
            int? argument = opCode == OpCodes.Ldarg_0 ? 0 :
                opCode == OpCodes.Ldarg_1 ? 1 :
                opCode == OpCodes.Ldarg_2 ? 2 :
                opCode == OpCodes.Ldarg_3 ? 3 :
                opCode == OpCodes.Ldarg || opCode == OpCodes.Ldarg_S ||
                opCode == OpCodes.Ldarga || opCode == OpCodes.Ldarga_S ||
                opCode == OpCodes.Starg || opCode == OpCodes.Starg_S
                    ? instruction.VariableIndex
                    : null;
            if (argument.HasValue && (argument.Value < 0 ||
                    argument.Value >= argumentCount)) return false;
            int? local = opCode == OpCodes.Ldloc_0 ||
                opCode == OpCodes.Stloc_0 ? 0 :
                opCode == OpCodes.Ldloc_1 || opCode == OpCodes.Stloc_1 ? 1 :
                opCode == OpCodes.Ldloc_2 || opCode == OpCodes.Stloc_2 ? 2 :
                opCode == OpCodes.Ldloc_3 || opCode == OpCodes.Stloc_3 ? 3 :
                opCode == OpCodes.Ldloc || opCode == OpCodes.Ldloc_S ||
                opCode == OpCodes.Ldloca || opCode == OpCodes.Ldloca_S ||
                opCode == OpCodes.Stloc || opCode == OpCodes.Stloc_S
                    ? instruction.VariableIndex
                    : null;
            return !local.HasValue || local.Value >= 0 &&
                local.Value < localCount;
        }

        private static IEnumerable<int> Successors(Instruction instruction,
            IReadOnlyList<Instruction> instructions)
        {
            FlowControl flow = instruction.OpCode.FlowControl;
            if (flow is FlowControl.Return or FlowControl.Throw)
                yield break;
            foreach (int target in instruction.BranchTargets)
                yield return target;
            if (flow == FlowControl.Branch) yield break;
            if (instruction.EndOffset < instructions[^1].EndOffset)
                yield return instruction.EndOffset;
        }

        private static (int Pop, int Push) StackDelta(
            MetadataReader metadata, Instruction instruction,
            MethodStackSignature enclosing)
        {
            OpCode opCode = instruction.OpCode;
            if (opCode == OpCodes.Call || opCode == OpCodes.Callvirt ||
                opCode == OpCodes.Newobj)
            {
                MethodStackSignature called = DecodeMethodToken(metadata,
                    instruction.Token ?? throw new BadImageFormatException(
                        "Token metodo assente."));
                int pop = called.Parameters;
                if (opCode != OpCodes.Newobj && called.HasThis) pop++;
                int push = opCode == OpCodes.Newobj || called.ReturnsValue ? 1 : 0;
                return (pop, push);
            }
            if (opCode == OpCodes.Ret)
                return (enclosing.ReturnsValue ? 1 : 0, 0);
            return (FixedPop(opCode.StackBehaviourPop),
                FixedPush(opCode.StackBehaviourPush));
        }

        private static int FixedPop(StackBehaviour value) => value switch
        {
            StackBehaviour.Pop0 => 0,
            StackBehaviour.Pop1 or StackBehaviour.Popi or
                StackBehaviour.Popref => 1,
            StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1 or
                StackBehaviour.Popi_popi or StackBehaviour.Popi_popi8 or
                StackBehaviour.Popi_popr4 or StackBehaviour.Popi_popr8 or
                StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi => 2,
            StackBehaviour.Popi_popi_popi or
                StackBehaviour.Popref_popi_popi or
                StackBehaviour.Popref_popi_popi8 or
                StackBehaviour.Popref_popi_popr4 or
                StackBehaviour.Popref_popi_popr8 or
                StackBehaviour.Popref_popi_popref => 3,
            StackBehaviour.Varpop => throw new BadImageFormatException(
                "Comportamento stack pop variabile non supportato."),
            _ => throw new BadImageFormatException(
                "Comportamento stack pop sconosciuto.")
        };

        private static int FixedPush(StackBehaviour value) => value switch
        {
            StackBehaviour.Push0 => 0,
            StackBehaviour.Push1 or StackBehaviour.Pushi or
                StackBehaviour.Pushi8 or StackBehaviour.Pushr4 or
                StackBehaviour.Pushr8 or StackBehaviour.Pushref => 1,
            StackBehaviour.Push1_push1 => 2,
            StackBehaviour.Varpush => throw new BadImageFormatException(
                "Comportamento stack push variabile non supportato."),
            _ => throw new BadImageFormatException(
                "Comportamento stack push sconosciuto.")
        };

        private static MethodStackSignature DecodeMethodToken(
            MetadataReader metadata, int token)
        {
            EntityHandle handle = MetadataTokens.EntityHandle(token);
            return handle.Kind switch
            {
                HandleKind.MethodDefinition => DecodeSignature(metadata,
                    metadata.GetMethodDefinition(
                        (MethodDefinitionHandle)handle).Signature),
                HandleKind.MemberReference => DecodeSignature(metadata,
                    metadata.GetMemberReference(
                        (MemberReferenceHandle)handle).Signature),
                HandleKind.MethodSpecification => DecodeMethodHandle(metadata,
                    metadata.GetMethodSpecification(
                        (MethodSpecificationHandle)handle).Method),
                _ => throw new BadImageFormatException(
                    "Token call non riferito a un metodo.")
            };
        }

        private static MethodStackSignature DecodeMethodHandle(
            MetadataReader metadata, EntityHandle handle) => handle.Kind switch
        {
            HandleKind.MethodDefinition => DecodeSignature(metadata,
                metadata.GetMethodDefinition(
                    (MethodDefinitionHandle)handle).Signature),
            HandleKind.MemberReference => DecodeSignature(metadata,
                metadata.GetMemberReference(
                    (MemberReferenceHandle)handle).Signature),
            _ => throw new BadImageFormatException(
                "Metodo generico con riferimento non valido.")
        };

        private static MethodStackSignature DecodeSignature(
            MetadataReader metadata, BlobHandle signature)
        {
            BlobReader reader = metadata.GetBlobReader(signature);
            SignatureHeader header = reader.ReadSignatureHeader();
            if (header.Kind != SignatureKind.Method)
                throw new BadImageFormatException("Firma metodo non valida.");
            if (header.IsGeneric && reader.ReadCompressedInteger() < 0)
                throw new BadImageFormatException("Arity generica non valida.");
            int parameters = reader.ReadCompressedInteger();
            if (parameters < 0)
                throw new BadImageFormatException("Numero parametri non valido.");
            bool returnsValue = ReadReturnsValue(ref reader);
            return new MethodStackSignature(parameters, header.IsInstance,
                returnsValue);
        }

        private static bool ReadReturnsValue(ref BlobReader reader)
        {
            while (true)
            {
                SignatureTypeCode code = reader.ReadSignatureTypeCode();
                if (code is SignatureTypeCode.RequiredModifier or
                    SignatureTypeCode.OptionalModifier)
                {
                    reader.ReadTypeHandle();
                    continue;
                }
                return code != SignatureTypeCode.Void;
            }
        }

        private static void ValidateToken(string moduleIdentity,
            MetadataReader metadata, int token, OperandType operandType,
            ICollection<DdfDotNetContractIssue> issues)
        {
            int table = (int)((uint)token >> 24);
            int row = token & 0x00ffffff;
            bool allowed = operandType switch
            {
                OperandType.InlineString => table == 0x70,
                OperandType.InlineSig => table == 0x11,
                OperandType.InlineField => table is 0x04 or 0x0a,
                OperandType.InlineMethod => table is 0x06 or 0x0a or 0x2b,
                OperandType.InlineType => table is 0x01 or 0x02 or 0x1b,
                OperandType.InlineTok => table is 0x01 or 0x02 or 0x04 or
                    0x06 or 0x0a or 0x1b or 0x2b,
                _ => false
            };
            if (!allowed || row <= 0)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC512",
                    moduleIdentity + ": token IL non valido."));
                return;
            }
            try
            {
                if (table == 0x70)
                {
                    metadata.GetUserString(MetadataTokens.UserStringHandle(row));
                    return;
                }
                byte tableValue = checked((byte)table);
                if (!Enum.IsDefined(typeof(TableIndex), tableValue) ||
                    row > metadata.GetTableRowCount((TableIndex)tableValue))
                    throw new BadImageFormatException();
            }
            catch (Exception exception) when (exception is BadImageFormatException ||
                                               exception is ArgumentOutOfRangeException)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC512",
                    moduleIdentity + ": token IL fuori dai metadati."));
            }
        }

        private static OpCode ReadOpCode(byte[] il, ref int position)
        {
            Ensure(il, position, 1);
            byte first = il[position++];
            if (first != 0xfe)
            {
                if (OneByte.TryGetValue(first, out OpCode opCode)) return opCode;
                throw new BadImageFormatException("Opcode IL sconosciuto.");
            }
            Ensure(il, position, 1);
            byte second = il[position++];
            if (TwoByte.TryGetValue(second, out OpCode extended)) return extended;
            throw new BadImageFormatException("Opcode IL esteso sconosciuto.");
        }

        private static bool ValidRange(int start, int length, int total,
            ISet<int> instructionOffsets)
        {
            if (start < 0 || length <= 0 || start > total - length ||
                !instructionOffsets.Contains(start)) return false;
            int end = start + length;
            return end == total || instructionOffsets.Contains(end);
        }

        private static void Ensure(byte[] value, int offset, int count)
        {
            if (offset < 0 || count < 0 || offset > value.Length - count)
                throw new BadImageFormatException("Corpo IL troncato.");
        }

        private static IReadOnlyDictionary<byte, OpCode> CreateOpCodes(
            bool twoByte)
        {
            var result = new Dictionary<byte, OpCode>();
            foreach (FieldInfo field in typeof(OpCodes).GetFields(
                         BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetValue(null) is not OpCode opCode ||
                    (opCode.Size == 2) != twoByte) continue;
                result[(byte)(opCode.Value & 0xff)] = opCode;
            }
            return result;
        }
    }
}
