using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DDFLanguageEditor.Compiler
{
    /// <summary>
    /// Stable capability identifiers used by compiled standalone DDF programs.
    /// A runner must explicitly advertise and provide every required capability.
    /// </summary>
    public static class DdfDotNetHostCapabilities
    {
        public const string FunctionPrefix = "host.function.";
        public const string ObjectReadPrefix = "host.object.read.";
        public const string ObjectWritePrefix = "host.object.write.";
        public const string MemberReadPrefix = "host.member.read.";
        public const string MemberWritePrefix = "host.member.write.";
        public const string ElementReadPrefix = "host.element.read.";
        public const string ElementWritePrefix = "host.element.write.";

        public static string ForFunction(string functionName) => Canonical(
            FunctionPrefix, Validate(functionName, nameof(functionName)));

        public static string ForObjectRead(string objectName) => Canonical(
            ObjectReadPrefix, Validate(objectName, nameof(objectName)));

        public static string ForObjectWrite(string objectName) => Canonical(
            ObjectWritePrefix, Validate(objectName, nameof(objectName)));

        public static string ForMemberRead(string objectName,
            params string[] memberPath) => Canonical(MemberReadPrefix,
                Path(objectName, memberPath));

        public static string ForMemberWrite(string objectName,
            params string[] memberPath) => Canonical(MemberWritePrefix,
                Path(objectName, memberPath));

        public static string ForElementRead(string objectName,
            params string[] memberPath) => Canonical(ElementReadPrefix,
                Path(objectName, memberPath));

        public static string ForElementWrite(string objectName,
            params string[] memberPath) => Canonical(ElementWritePrefix,
                Path(objectName, memberPath));

        private static string Path(string objectName, IEnumerable<string> members)
        {
            string root = Validate(objectName, nameof(objectName));
            string[] path = (members ?? Array.Empty<string>()).Select(member =>
                Validate(member, nameof(members))).ToArray();
            return path.Length == 0 ? root : root + "." + string.Join(".", path);
        }

        private static string Validate(string value, string parameterName)
        {
            string name = (value ?? string.Empty).Trim();
            if (name.Length == 0 ||
                !(char.IsLetter(name[0]) || name[0] == '_') ||
                name.Skip(1).Any(character => !(char.IsLetterOrDigit(character) ||
                    character == '_' || character == '.')))
                throw new ArgumentException(
                    "Invalid DDF host capability name.", parameterName);
            return name;
        }

        private static string Canonical(string prefix, string identity)
        {
            string slug = new string(identity.ToLowerInvariant().Select(character =>
                    character >= 'a' && character <= 'z' ||
                    character >= '0' && character <= '9'
                        ? character : '-').ToArray()).Trim('-');
            if (slug.Length == 0) slug = "function";
            string hash = Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
            return prefix + slug + "-" + hash;
        }
    }
}
