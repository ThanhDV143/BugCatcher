using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThanhDV.BugCatcher.Editor
{
    /// <summary>
    /// Reads the ECMA-335 metadata of a compiled .NET assembly from its bytes, without loading it.
    /// </summary>
    /// <remarks>
    /// Used to list the [ConsoleMethod] methods of the player assemblies a build compiled, which the Editor has not
    /// loaded when the build has defines the Editor does not. Supports PE32 and PE32+ images, the compressed (#~),
    /// uncompressed (#-) and #JTD table streams, 2- and 4-byte heap and table indexes, the indirection tables
    /// (MethodPtr and the other *Ptr tables), nested types and attributes defined in the same assembly or another.
    /// Every read is bounds-checked: a malformed file throws <see cref="BadImageFormatException"/>.
    /// </remarks>
    internal sealed class AssemblyMetadata
    {
        // --- Table numbers (ECMA-335 II.22) -------------------------------------------------------

        private const int Module = 0x00, TypeRef = 0x01, TypeDef = 0x02, FieldPtr = 0x03, Field = 0x04,
            MethodPtr = 0x05, MethodDef = 0x06, ParamPtr = 0x07, Param = 0x08, InterfaceImpl = 0x09,
            MemberRef = 0x0A, Constant = 0x0B, CustomAttribute = 0x0C, FieldMarshal = 0x0D, DeclSecurity = 0x0E,
            ClassLayout = 0x0F, FieldLayout = 0x10, StandAloneSig = 0x11, EventMap = 0x12, EventPtr = 0x13,
            Event = 0x14, PropertyMap = 0x15, PropertyPtr = 0x16, Property = 0x17, MethodSemantics = 0x18,
            MethodImpl = 0x19, ModuleRef = 0x1A, TypeSpec = 0x1B, ImplMap = 0x1C, FieldRva = 0x1D, EncLog = 0x1E,
            EncMap = 0x1F, AssemblyDef = 0x20, AssemblyProcessor = 0x21, AssemblyOS = 0x22, AssemblyRef = 0x23,
            AssemblyRefProcessor = 0x24, AssemblyRefOS = 0x25, FileDef = 0x26, ExportedType = 0x27,
            ManifestResource = 0x28, NestedClass = 0x29, GenericParam = 0x2A, MethodSpec = 0x2B,
            GenericParamConstraint = 0x2C;

        private const int TableCount = 0x2D;

        // --- Column kinds ---------------------------------------------------------------------------
        // 0 .. TableCount-1: index into that table. CodedBase + n: coded index n. ListBase + table: start of a run of
        // rows of that table, which goes through its *Ptr table when that table is present.

        private const int U16 = -1, U32 = -2, Str = -3, Guid = -4, Blob = -5;
        private const int CodedBase = 0x40, ListBase = 0x80;

        private const int TypeDefOrRef = 0, HasConstant = 1, HasCustomAttribute = 2, HasFieldMarshal = 3,
            HasDeclSecurity = 4, MemberRefParent = 5, HasSemantics = 6, MethodDefOrRef = 7, MemberForwarded = 8,
            Implementation = 9, CustomAttributeType = 10, ResolutionScope = 11, TypeOrMethodDef = 12;

        /// <summary>Tables of each coded index, in tag order (II.24.2.6); -1 is an unused tag.</summary>
        private static readonly int[][] CodedTables =
        {
            new[] { TypeDef, TypeRef, TypeSpec },
            new[] { Field, Param, Property },
            new[]
            {
                MethodDef, Field, TypeRef, TypeDef, Param, InterfaceImpl, MemberRef, Module, DeclSecurity, Property,
                Event, StandAloneSig, ModuleRef, TypeSpec, AssemblyDef, AssemblyRef, FileDef, ExportedType,
                ManifestResource, GenericParam, GenericParamConstraint, MethodSpec
            },
            new[] { Field, Param },
            new[] { TypeDef, MethodDef, AssemblyDef },
            new[] { TypeDef, TypeRef, ModuleRef, MethodDef, TypeSpec },
            new[] { Event, Property },
            new[] { MethodDef, MemberRef },
            new[] { Field, MethodDef },
            new[] { FileDef, AssemblyRef, ExportedType },
            new[] { -1, -1, MethodDef, MemberRef, -1 },
            new[] { Module, ModuleRef, AssemblyRef, TypeRef },
            new[] { TypeDef, MethodDef }
        };

        /// <summary>Columns of every table (II.22), so the tables that follow can be located.</summary>
        private static readonly int[][] Schema = BuildSchema();

        private static int[][] BuildSchema()
        {
            int C(int coded) => CodedBase + coded;
            int L(int table) => ListBase + table;

            var schema = new int[TableCount][];
            schema[Module] = new[] { U16, Str, Guid, Guid, Guid };
            schema[TypeRef] = new[] { C(ResolutionScope), Str, Str };
            schema[TypeDef] = new[] { U32, Str, Str, C(TypeDefOrRef), L(Field), L(MethodDef) };
            schema[FieldPtr] = new[] { Field };
            schema[Field] = new[] { U16, Str, Blob };
            schema[MethodPtr] = new[] { MethodDef };
            schema[MethodDef] = new[] { U32, U16, U16, Str, Blob, L(Param) };
            schema[ParamPtr] = new[] { Param };
            schema[Param] = new[] { U16, U16, Str };
            schema[InterfaceImpl] = new[] { TypeDef, C(TypeDefOrRef) };
            schema[MemberRef] = new[] { C(MemberRefParent), Str, Blob };
            // Type is one byte followed by a padding byte.
            schema[Constant] = new[] { U16, C(HasConstant), Blob };
            schema[CustomAttribute] = new[] { C(HasCustomAttribute), C(CustomAttributeType), Blob };
            schema[FieldMarshal] = new[] { C(HasFieldMarshal), Blob };
            schema[DeclSecurity] = new[] { U16, C(HasDeclSecurity), Blob };
            schema[ClassLayout] = new[] { U16, U32, TypeDef };
            schema[FieldLayout] = new[] { U32, Field };
            schema[StandAloneSig] = new[] { Blob };
            schema[EventMap] = new[] { TypeDef, L(Event) };
            schema[EventPtr] = new[] { Event };
            schema[Event] = new[] { U16, Str, C(TypeDefOrRef) };
            schema[PropertyMap] = new[] { TypeDef, L(Property) };
            schema[PropertyPtr] = new[] { Property };
            schema[Property] = new[] { U16, Str, Blob };
            schema[MethodSemantics] = new[] { U16, MethodDef, C(HasSemantics) };
            schema[MethodImpl] = new[] { TypeDef, C(MethodDefOrRef), C(MethodDefOrRef) };
            schema[ModuleRef] = new[] { Str };
            schema[TypeSpec] = new[] { Blob };
            schema[ImplMap] = new[] { U16, C(MemberForwarded), Str, ModuleRef };
            schema[FieldRva] = new[] { U32, Field };
            schema[EncLog] = new[] { U32, U32 };
            schema[EncMap] = new[] { U32 };
            schema[AssemblyDef] = new[] { U32, U16, U16, U16, U16, U32, Blob, Str, Str };
            schema[AssemblyProcessor] = new[] { U32 };
            schema[AssemblyOS] = new[] { U32, U32, U32 };
            schema[AssemblyRef] = new[] { U16, U16, U16, U16, U32, Blob, Str, Str, Blob };
            schema[AssemblyRefProcessor] = new[] { U32, AssemblyRef };
            schema[AssemblyRefOS] = new[] { U32, U32, U32, AssemblyRef };
            schema[FileDef] = new[] { U32, Str, Blob };
            schema[ExportedType] = new[] { U32, U32, Str, Str, C(Implementation) };
            schema[ManifestResource] = new[] { U32, U32, Str, C(Implementation) };
            schema[NestedClass] = new[] { TypeDef, TypeDef };
            schema[GenericParam] = new[] { U16, U16, C(TypeOrMethodDef), Str };
            schema[MethodSpec] = new[] { C(MethodDefOrRef), Blob };
            schema[GenericParamConstraint] = new[] { GenericParam, C(TypeDefOrRef) };
            return schema;
        }

        /// <summary>Ptr table that a list column goes through, or -1.</summary>
        private static int PtrTable(int table)
        {
            switch (table)
            {
                case Field: return FieldPtr;
                case MethodDef: return MethodPtr;
                case Param: return ParamPtr;
                case Event: return EventPtr;
                case Property: return PropertyPtr;
                default: return -1;
            }
        }

        // --- State ------------------------------------------------------------------------------------

        private readonly byte[] data;
        private int stringsStart, stringsSize;
        private readonly int[] rows = new int[TableCount];
        private readonly int[] tableStart = new int[TableCount];
        private readonly int[] rowSize = new int[TableCount];
        private readonly int[][] columnOffset = new int[TableCount][];
        private readonly int[][] columnSize = new int[TableCount][];

        /// <summary>For each MethodDef row (1-based), the TypeDef row that declares it; 0 if none.</summary>
        private int[] methodOwner;
        /// <summary>For each TypeDef row (1-based), the TypeDef row that encloses it; 0 for a top-level type.</summary>
        private int[] enclosingType;

        /// <summary>Simple name of the assembly; null for a module without an assembly manifest.</summary>
        public string Name { get; private set; }

        /// <summary>Simple names of the referenced assemblies.</summary>
        public IReadOnlyList<string> References { get; private set; }

        private AssemblyMetadata(byte[] data)
        {
            this.data = data;
        }

        /// <summary>Returns null if the file is not a .NET image (a native library, for example).</summary>
        /// <exception cref="BadImageFormatException">The file is a malformed or unsupported .NET image.</exception>
        /// <exception cref="IOException">The file could not be read.</exception>
        public static AssemblyMetadata Read(string path)
        {
            byte[] bytes;
            // The build may still hold the file open.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length > int.MaxValue) throw new BadImageFormatException("File too large", path);
                bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int n = stream.Read(bytes, read, bytes.Length - read);
                    if (n == 0) throw new IOException("Unexpected end of file: " + path);
                    read += n;
                }
            }
            return Parse(bytes, path);
        }

        /// <summary>Returns null if the bytes are not a .NET image.</summary>
        /// <exception cref="BadImageFormatException">Malformed or unsupported .NET image.</exception>
        public static AssemblyMetadata Parse(byte[] bytes, string fileName = null)
        {
            var metadata = new AssemblyMetadata(bytes);
            try
            {
                return metadata.Load() ? metadata : null;
            }
            catch (BadImageFormatException e) when (e.FileName == null && fileName != null)
            {
                throw new BadImageFormatException(e.Message, fileName);
            }
        }

        /// <summary>
        /// Methods marked with the attribute whose type has this full name: "Namespace.Type", or
        /// "Namespace.Outer/Inner" for a nested type. Returns each method's declaring type, written the same way,
        /// and its name. A method marked more than once is returned once.
        /// </summary>
        public List<(string Type, string Method)> MethodsWithAttribute(string attributeFullName)
        {
            var result = new List<(string, string)>();
            var markedMethods = new HashSet<int>();
            var isMatch = new Dictionary<uint, bool>();
            int parentTagBits = TagBits(HasCustomAttribute), typeTagBits = TagBits(CustomAttributeType);

            for (int row = 1; row <= rows[CustomAttribute]; row++)
            {
                uint parent = Column(CustomAttribute, row, 0);
                if ((parent & ((1u << parentTagBits) - 1)) != 0) continue; // tag 0: MethodDef
                int method = (int)(parent >> parentTagBits);
                if (method < 1 || method > rows[MethodDef] || markedMethods.Contains(method)) continue;

                uint constructor = Column(CustomAttribute, row, 1);
                if (!isMatch.TryGetValue(constructor, out bool match))
                {
                    match = AttributeTypeName(constructor, typeTagBits) == attributeFullName;
                    isMatch.Add(constructor, match);
                }
                if (!match) continue;

                int owner = methodOwner[method];
                if (owner == 0) continue; // <Module> globals cannot carry a command and are always kept.
                markedMethods.Add(method);
                result.Add((TypeDefName(owner), ReadString(Column(MethodDef, method, 3))));
            }
            return result;
        }

        // --- Loading ----------------------------------------------------------------------------------

        private bool Load()
        {
            // DOS header and PE signature (II.25.2.1).
            if (data.Length < 0x40 || U16At(0) != 0x5A4D) return false;
            int pe = I32At(0x3C);
            if (pe < 0 || pe > data.Length - 24 || U32At(pe) != 0x00004550) return false;

            // COFF header and optional header (II.25.2.2, II.25.2.3).
            int coff = pe + 4;
            int sectionCount = U16At(coff + 2);
            int optionalSize = U16At(coff + 16);
            int optional = coff + 20;
            int directories;
            switch (U16At(optional))
            {
                case 0x10B: directories = optional + 96; break; // PE32
                case 0x20B: directories = optional + 112; break; // PE32+
                default: return false;
            }
            Require(directories <= optional + optionalSize, "Optional header too small");
            int directoryCount = I32At(directories - 4);
            if (directoryCount < 15 || directories + 15 * 8 > optional + optionalSize) return false;
            int cliRva = I32At(directories + 14 * 8);
            if (cliRva == 0) return false; // Native image: no CLI header.

            int sections = optional + optionalSize;
            int CliOffset(int rva) => RvaToOffset(rva, sections, sectionCount);

            // CLI header (II.25.3.3) and metadata root (II.24.2.1).
            int cli = CliOffset(cliRva);
            int root = CliOffset(I32At(cli + 8));
            int rootSize = I32At(cli + 12);
            Require(rootSize > 0 && root <= data.Length - rootSize, "Metadata outside the file");
            Require(U32At(root) == 0x424A5342, "Bad metadata signature");
            int versionLength = I32At(root + 12);
            Require(versionLength >= 0 && versionLength <= rootSize - 20, "Bad metadata version length");
            int position = root + 16 + versionLength + 2;
            int streamCount = U16At(position);
            position += 2;

            // Stream headers (II.24.2.2).
            int tables = -1, tablesSize = 0;
            bool uncompressed = false, forceLargeIndexes = false;
            for (int i = 0; i < streamCount; i++)
            {
                int offset = I32At(position);
                int size = I32At(position + 4);
                position += 8;
                int nameStart = position;
                while (ByteAt(position) != 0) position++;
                string name = Encoding.ASCII.GetString(data, nameStart, position - nameStart);
                position = nameStart + ((position - nameStart + 4) & ~3);
                Require(offset >= 0 && size >= 0 && offset <= rootSize && size <= rootSize - offset, "Stream " + name + " outside the metadata");

                switch (name)
                {
                    case "#~":
                    case "#-":
                        tables = root + offset;
                        tablesSize = size;
                        uncompressed = name == "#-";
                        break;
                    case "#Strings":
                        stringsStart = root + offset;
                        stringsSize = size;
                        break;
                    case "#JTD":
                        // Marks metadata whose indexes are all 4 bytes.
                        forceLargeIndexes = true;
                        break;
                }
            }
            Require(tables >= 0, "No metadata table stream");
            ReadTables(tables, tablesSize, uncompressed, forceLargeIndexes);
            return true;
        }

        private int RvaToOffset(int rva, int sections, int sectionCount)
        {
            for (int i = 0; i < sectionCount; i++)
            {
                int header = sections + i * 40;
                int virtualSize = I32At(header + 8);
                int virtualAddress = I32At(header + 12);
                int rawSize = I32At(header + 16);
                int rawPointer = I32At(header + 20);
                int extent = Math.Max(virtualSize, rawSize);
                if (rva >= virtualAddress && rva - virtualAddress < extent)
                {
                    int offset = rva - virtualAddress + rawPointer;
                    Require(offset >= 0 && offset < data.Length, "RVA outside the file");
                    return offset;
                }
            }
            throw new BadImageFormatException("RVA 0x" + rva.ToString("X") + " is in no section");
        }

        private void ReadTables(int start, int size, bool uncompressed, bool forceLargeIndexes)
        {
            // Table stream header (II.24.2.6).
            int end = start + size;
            int heapSizes = ByteAt(start + 6);
            ulong valid = (ulong)U32At(start + 8) | ((ulong)U32At(start + 12) << 32);
            int position = start + 24;
            for (int table = 0; table < 64; table++)
            {
                if ((valid & (1UL << table)) == 0) continue;
                Require(table < TableCount, "Unknown metadata table 0x" + table.ToString("X2"));
                int count = I32At(position);
                Require(count >= 0 && count < 0x1000000, "Bad row count");
                rows[table] = count;
                position += 4;
            }
            // Uncompressed streams may carry 4 extra bytes (flag 0x40), and edit-and-continue deltas (flag 0x20)
            // use 4-byte indexes everywhere.
            if (uncompressed && (heapSizes & 0x40) != 0) position += 4;
            if (uncompressed && (heapSizes & 0x20) != 0) forceLargeIndexes = true;

            int stringIndex = (heapSizes & 0x01) != 0 ? 4 : 2;
            int guidIndex = (heapSizes & 0x02) != 0 ? 4 : 2;
            int blobIndex = (heapSizes & 0x04) != 0 ? 4 : 2;

            for (int table = 0; table < TableCount; table++)
            {
                int[] columns = Schema[table];
                columnOffset[table] = new int[columns.Length];
                columnSize[table] = new int[columns.Length];
                int offset = 0;
                for (int c = 0; c < columns.Length; c++)
                {
                    int width;
                    int kind = columns[c];
                    if (kind == U16) width = 2;
                    else if (kind == U32) width = 4;
                    else if (kind == Str) width = stringIndex;
                    else if (kind == Guid) width = guidIndex;
                    else if (kind == Blob) width = blobIndex;
                    else if (forceLargeIndexes) width = 4;
                    else if (kind >= ListBase)
                    {
                        int target = kind - ListBase;
                        int ptr = PtrTable(target);
                        width = IndexSize(Math.Max(rows[target], ptr >= 0 ? rows[ptr] : 0));
                    }
                    else if (kind >= CodedBase) width = CodedIndexSize(kind - CodedBase);
                    else width = IndexSize(rows[kind]);

                    columnOffset[table][c] = offset;
                    columnSize[table][c] = width;
                    offset += width;
                }
                rowSize[table] = offset;
                tableStart[table] = position;
                long tableEnd = position + (long)offset * rows[table];
                Require(tableEnd <= end, "Metadata table 0x" + table.ToString("X2") + " outside its stream");
                position = (int)tableEnd;
            }

            Name = rows[AssemblyDef] > 0 ? ReadString(Column(AssemblyDef, 1, 7)) : null;
            var references = new string[rows[AssemblyRef]];
            for (int row = 1; row <= references.Length; row++) references[row - 1] = ReadString(Column(AssemblyRef, row, 6));
            References = references;

            MapMethodOwners();
            MapEnclosingTypes();
        }

        private static int IndexSize(int rowCount) => rowCount < 0x10000 ? 2 : 4;

        private int CodedIndexSize(int coded)
        {
            int max = 0;
            foreach (int table in CodedTables[coded])
                if (table >= 0) max = Math.Max(max, rows[table]);
            return max < (1 << (16 - TagBits(coded))) ? 2 : 4;
        }

        private static int TagBits(int coded)
        {
            int bits = 0;
            while ((1 << bits) < CodedTables[coded].Length) bits++;
            return bits;
        }

        /// <summary>A TypeDef owns the run of methods from its MethodList to the next TypeDef's (II.22.37).</summary>
        private void MapMethodOwners()
        {
            methodOwner = new int[rows[MethodDef] + 1];
            bool indirect = rows[MethodPtr] > 0;
            int listLength = indirect ? rows[MethodPtr] : rows[MethodDef];
            int typeCount = rows[TypeDef];
            for (int type = 1; type <= typeCount; type++)
            {
                int first = (int)Column(TypeDef, type, 5);
                int next = type < typeCount ? (int)Column(TypeDef, type + 1, 5) : listLength + 1;
                first = Math.Max(first, 1);
                next = Math.Min(next, listLength + 1);
                for (int entry = first; entry < next; entry++)
                {
                    int method = indirect ? (int)Column(MethodPtr, entry, 0) : entry;
                    if (method >= 1 && method <= rows[MethodDef]) methodOwner[method] = type;
                }
            }
        }

        private void MapEnclosingTypes()
        {
            enclosingType = new int[rows[TypeDef] + 1];
            for (int row = 1; row <= rows[NestedClass]; row++)
            {
                int nested = (int)Column(NestedClass, row, 0);
                int enclosing = (int)Column(NestedClass, row, 1);
                if (nested >= 1 && nested <= rows[TypeDef] && enclosing >= 1 && enclosing <= rows[TypeDef])
                    enclosingType[nested] = enclosing;
            }
        }

        // --- Names ------------------------------------------------------------------------------------

        /// <summary>Full name of the type that declares an attribute constructor, or null if it is not a plain type.</summary>
        private string AttributeTypeName(uint constructor, int tagBits)
        {
            uint tag = constructor & ((1u << tagBits) - 1);
            int row = (int)(constructor >> tagBits);
            switch (CodedTables[CustomAttributeType][tag])
            {
                case MethodDef:
                    if (row < 1 || row > rows[MethodDef] || methodOwner[row] == 0) return null;
                    return TypeDefName(methodOwner[row]);
                case MemberRef:
                    if (row < 1 || row > rows[MemberRef]) return null;
                    uint parent = Column(MemberRef, row, 0);
                    int parentBits = TagBits(MemberRefParent);
                    int parentRow = (int)(parent >> parentBits);
                    switch (CodedTables[MemberRefParent][parent & ((1u << parentBits) - 1)])
                    {
                        case TypeDef: return parentRow >= 1 && parentRow <= rows[TypeDef] ? TypeDefName(parentRow) : null;
                        case TypeRef: return parentRow >= 1 && parentRow <= rows[TypeRef] ? TypeRefName(parentRow) : null;
                        default: return null; // TypeSpec (generic attribute), ModuleRef, MethodDef: not a plain type.
                    }
                default:
                    return null;
            }
        }

        /// <summary>"Namespace.Type", or "Namespace.Outer/Inner" for a nested type, as in link.xml.</summary>
        private string TypeDefName(int row)
        {
            var parts = new List<string>();
            for (int depth = 0; row != 0; depth++)
            {
                Require(depth < 64, "Cycle in nested types");
                parts.Add(Qualified(ReadString(Column(TypeDef, row, 2)), ReadString(Column(TypeDef, row, 1))));
                row = enclosingType[row];
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private string TypeRefName(int row)
        {
            var parts = new List<string>();
            int scopeBits = TagBits(ResolutionScope);
            for (int depth = 0; ; depth++)
            {
                Require(depth < 64, "Cycle in nested type references");
                parts.Add(Qualified(ReadString(Column(TypeRef, row, 2)), ReadString(Column(TypeRef, row, 1))));
                uint scope = Column(TypeRef, row, 0);
                int scopeRow = (int)(scope >> scopeBits);
                // Tag 3: the type is nested in another TypeRef.
                if (CodedTables[ResolutionScope][scope & ((1u << scopeBits) - 1)] != TypeRef || scopeRow < 1 || scopeRow > rows[TypeRef]) break;
                row = scopeRow;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string Qualified(string ns, string name) => string.IsNullOrEmpty(ns) ? name : ns + "." + name;

        // --- Raw reads --------------------------------------------------------------------------------

        private uint Column(int table, int row, int column)
        {
            Require(row >= 1 && row <= rows[table], "Row outside table 0x" + table.ToString("X2"));
            int offset = tableStart[table] + (row - 1) * rowSize[table] + columnOffset[table][column];
            return columnSize[table][column] == 2 ? U16At(offset) : U32At(offset);
        }

        /// <summary>A null-terminated UTF-8 string from the #Strings heap.</summary>
        private string ReadString(uint index)
        {
            Require(index < stringsSize || (index == 0 && stringsSize == 0), "String outside the #Strings heap");
            if (stringsSize == 0) return "";
            int start = stringsStart + (int)index;
            int end = start;
            int limit = stringsStart + stringsSize;
            while (end < limit && data[end] != 0) end++;
            Require(end < limit, "Unterminated string");
            return Encoding.UTF8.GetString(data, start, end - start);
        }

        private byte ByteAt(int offset)
        {
            Require(offset >= 0 && offset < data.Length, "Read outside the file");
            return data[offset];
        }

        private ushort U16At(int offset)
        {
            Require(offset >= 0 && offset <= data.Length - 2, "Read outside the file");
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private uint U32At(int offset)
        {
            Require(offset >= 0 && offset <= data.Length - 4, "Read outside the file");
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        private int I32At(int offset) => (int)U32At(offset);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new BadImageFormatException(message);
        }
    }
}
