﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Assent;
using BTDB.Buffer;
using BTDB.FieldHandler;
using BTDB.KVDBLayer;
using BTDB.ODBLayer;
using Xunit;

namespace BTDBTest
{
    public class ODBIteratorTest : IDisposable
    {
        readonly IKeyValueDB _lowDb;
        IObjectDB _db;

        public ODBIteratorTest()
        {
            _lowDb = new InMemoryKeyValueDB();
            OpenDb();
        }

        void ReopenDb()
        {
            _db.Dispose();
            OpenDb();
        }

        void OpenDb()
        {
            _db = new ObjectDB();
            _db.Open(_lowDb, false);
        }

        public class Duty
        {
            public string Name { get; set; }
        }

        public class Job
        {
            public Duty Duty { get; set; }
        }

        public class JobMap
        {
            public IDictionary<ulong, Job> Jobs { get; set; }
        }

        internal class ToStringFastVisitor : IODBFastVisitor
        {
            protected readonly StringBuilder Builder = new StringBuilder();
            public ByteBuffer Keys = ByteBuffer.NewEmpty();

            public override string ToString()
            {
                return Builder.ToString();
            }

            public void MarkCurrentKeyAsUsed(IKeyValueDBTransaction tr)
            {
                Keys = Keys.ResizingAppend(ByteBuffer.NewSync(tr.GetKeyPrefix())).ResizingAppend(tr.GetKey());
                Builder.Append("Used key: ");
                Print(ByteBuffer.NewSync(tr.GetKeyPrefix()));
                Builder.Append('|');
                Print(tr.GetKey());
                Builder.AppendFormat(" Value len:{0}", tr.GetStorageSizeOfCurrentKey().Value);
                Builder.AppendLine();
            }

            void Print(ByteBuffer b)
            {
                for (int i = 0; i < b.Length; i++)
                {
                    if (i > 0) Builder.Append(' ');
                    Builder.Append(b[i].ToString("X2"));
                }
            }
        }

        internal class ToStringVisitor : ToStringFastVisitor, IODBVisitor
        {
            public bool VisitSingleton(uint tableId, string tableName, ulong oid)
            {
                Builder.AppendFormat("Singleton {0}-{1} oid:{2}", tableId, tableName ?? "?Unknown?", oid);
                Builder.AppendLine();
                return true;
            }

            public bool StartObject(ulong oid, uint tableId, string tableName, uint version)
            {
                Builder.AppendFormat("Object oid:{0} {1}-{2} version:{3}", oid, tableId, tableName ?? "?Unknown?",
                    version);
                Builder.AppendLine();
                return true;
            }

            public bool StartField(string name)
            {
                Builder.AppendLine($"StartField {name}");
                return true;
            }

            public bool NeedScalarAsObject()
            {
                return true;
            }

            public void ScalarAsObject(object content)
            {
                Builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "ScalarObj {0}", content));
            }

            public bool NeedScalarAsText()
            {
                return true;
            }

            public void ScalarAsText(string content)
            {
                Builder.AppendLine($"ScalarStr {content}");
            }

            public void OidReference(ulong oid)
            {
                Builder.AppendLine($"OidReference {oid}");
            }

            public bool StartInlineObject(uint tableId, string tableName, uint version)
            {
                Builder.AppendLine($"StartInlineObject {tableId}-{tableName}-{version}");
                return true;
            }

            public void EndInlineObject()
            {
                Builder.AppendLine("EndInlineObject");
            }

            public bool StartList()
            {
                Builder.AppendLine("StartList");
                return true;
            }

            public bool StartItem()
            {
                Builder.AppendLine("StartItem");
                return true;
            }

            public void EndItem()
            {
                Builder.AppendLine("EndItem");
            }

            public void EndList()
            {
                Builder.AppendLine("EndList");
            }

            public bool StartDictionary()
            {
                Builder.AppendLine("StartDictionary");
                return true;
            }

            public bool StartDictKey()
            {
                Builder.AppendLine("StartDictKey");
                return true;
            }

            public void EndDictKey()
            {
                Builder.AppendLine("EndDictKey");
            }

            public bool StartDictValue()
            {
                Builder.AppendLine("StartDictValue");
                return true;
            }

            public void EndDictValue()
            {
                Builder.AppendLine("EndDictValue");
            }

            public void EndDictionary()
            {
                Builder.AppendLine("EndDictionary");
            }

            public void EndField()
            {
                Builder.AppendLine("EndField");
            }

            public void EndObject()
            {
                Builder.AppendLine("EndObject");
            }

            public bool StartRelation(string relationName)
            {
                Builder.AppendLine($"Relation {relationName}");
                return true;
            }

            public bool StartRelationKey()
            {
                Builder.AppendLine("BeginKey");
                return true;
            }

            public void EndRelationKey()
            {
                Builder.AppendLine("EndKey");
            }

            public bool StartRelationValue()
            {
                Builder.AppendLine("BeginValue");
                return true;
            }

            public void EndRelationValue()
            {
                Builder.AppendLine("EndValue");
            }

            public void EndRelation()
            {
                Builder.AppendLine("EndRelation");
            }

            public void InlineBackRef(int iid)
            {
                Builder.AppendLine($"Inline back ref {iid}");
            }

            public void InlineRef(int iid)
            {
                Builder.AppendLine($"Inline ref {iid}");
            }
        }

        [Fact]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Basics()
        {
            using (var tr = _db.StartTransaction())
            {
                var jobs = tr.Singleton<JobMap>();
                jobs.Jobs[1] = new Job { Duty = new Duty { Name = "HardCore Code" } };
                tr.Commit();
            }
            IterateWithApprove();
        }

        public class DutyWithKey
        {
            [PrimaryKey]
            public ulong Id { get; set; }
            public string Name { get; set; }
        }

        public interface ISimpleDutyRelation
        {
            void Insert(DutyWithKey duty);
            bool RemoveById(ulong id);
        }

        [Fact]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void BasicsRelation()
        {
            using (var tr = _db.StartTransaction())
            {
                var creator = tr.InitRelation<ISimpleDutyRelation>("SimpleDutyRelation");
                var personSimpleTable = creator(tr);
                var duty = new DutyWithKey { Id = 1, Name = "HardCore Code" };
                personSimpleTable.Insert(duty);
                tr.Commit();
            }
            IterateWithApprove();
        }

        public enum TestEnum
        {
            Item1,
            Item2
        }

        public class VariousFieldTypes
        {
            public string StringField { get; set; }
            public sbyte SByteField { get; set; }
            public byte ByteField { get; set; }
            public short ShortField { get; set; }
            public ushort UShortField { get; set; }
            public int IntField { get; set; }
            public uint UIntField { get; set; }
            public long LongField { get; set; }
            public ulong ULongField { get; set; }
            public IIndirect<object> DbObjectField { get; set; }
            public IIndirect<VariousFieldTypes> VariousFieldTypesField { get; set; }
            public bool BoolField { get; set; }
            public double DoubleField { get; set; }
            public float FloatField { get; set; }
            public decimal DecimalField { get; set; }
            public Guid GuidField { get; set; }
            public DateTime DateTimeField { get; set; }
            public TimeSpan TimeSpanField { get; set; }
            public TestEnum EnumField { get; set; }
            public byte[] ByteArrayField { get; set; }
            public ByteBuffer ByteBufferField { get; set; }
        }

        [Fact]
        public void FieldsOfVariousTypes()
        {
            using (var tr = _db.StartTransaction())
            {
                var o = tr.Singleton<VariousFieldTypes>();
                o.StringField = "Text";
                o.SByteField = -10;
                o.ByteField = 10;
                o.ShortField = -1000;
                o.UShortField = 1000;
                o.IntField = -100000;
                o.UIntField = 100000;
                o.LongField = -1000000000000;
                o.ULongField = 1000000000000;
                o.DbObjectField = new DBIndirect<object>(o);
                o.VariousFieldTypesField = new DBIndirect<VariousFieldTypes>(o);
                o.BoolField = true;
                o.DoubleField = 12.34;
                o.FloatField = -12.34f;
                o.DecimalField = 123456.789m;
                o.DateTimeField = new DateTime(2000, 1, 1, 12, 34, 56, DateTimeKind.Local);
                o.TimeSpanField = new TimeSpan(1, 2, 3, 4);
                o.GuidField = new Guid("39aabab2-9971-4113-9998-a30fc7d5606a");
                o.EnumField = TestEnum.Item2;
                o.ByteArrayField = new byte[] { 0, 1, 2 };
                o.ByteBufferField = ByteBuffer.NewAsync(new byte[] { 0, 1, 2 }, 1, 1);
                tr.Commit();
            }
            IterateWithApprove();
        }

        void IterateWithApprove([CallerMemberName] string testName = null)
        {
            using (var tr = _db.StartTransaction())
            {
                var fastVisitor = new ToStringFastVisitor();
                var visitor = new ToStringVisitor();
                var iterator = new ODBIterator(tr, fastVisitor);
                iterator.Iterate();
                iterator = new ODBIterator(tr, visitor);
                iterator.Iterate();
                var text = visitor.ToString();
                this.Assent(text, null, testName);
                Assert.Equal(fastVisitor.Keys.ToByteArray(), visitor.Keys.ToByteArray());
            }
        }

        public class VariousLists
        {
            public IList<int> IntList { get; set; }
            public IList<string> StringList { get; set; }
            public IList<byte> ByteList { get; set; }
        }

        [Fact]
        public void ListOfSimpleValues()
        {
            using (var tr = _db.StartTransaction())
            {
                var root = tr.Singleton<VariousLists>();
                root.IntList = new List<int> { 5, 10, 2000 };
                root.StringList = new List<string> { "A", null, "AB!" };
                root.ByteList = new List<byte> { 0, 255 };
                tr.Commit();
            }
            IterateWithApprove();
        }

        public class InlineDictionary
        {
            public Dictionary<int, string> Int2String { get; set; }
        }

        [Fact]
        public void InlineDictionariesOfSimpleValues()
        {
            using (var tr = _db.StartTransaction())
            {
                var root = tr.Singleton<InlineDictionary>();
                root.Int2String = new Dictionary<int, string> { { 1, "one" }, { 0, null } };
                tr.Commit();
            }
            IterateWithApprove();
        }

        public class Rule1
        {
            public string Name { get; set; }
        }

        public class Rule2
        {
            public string Name { get; set; }
            public int Type { get; set; }
        }

        public class ObjectWfd1
        {
            public Rule1 A { get; set; }
            public Rule1 B { get; set; }
            public Rule1 C { get; set; }
        }

        public class ObjectWfd2
        {
            public Rule2 A { get; set; }
            //public Rule2 B { get; set; }
            public Rule2 C { get; set; }
        }

        [Fact]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void UpgradeDeletedInlineObjectWorks()
        {
            var typeNameWfd = _db.RegisterType(typeof(ObjectWfd1));
            var typeNameRule = _db.RegisterType(typeof(Rule1));

            using (var tr = _db.StartTransaction())
            {
                var wfd = tr.Singleton<ObjectWfd1>();
                wfd.A = new Rule1 { Name = "A" };
                wfd.B = new Rule1 { Name = "B" };
                wfd.C = new Rule1 { Name = "C" };
                tr.Commit();
            }
            ReopenDb();
            _db.RegisterType(typeof(ObjectWfd2), typeNameWfd);
            _db.RegisterType(typeof(Rule2), typeNameRule);

            using (var tr = _db.StartTransaction())
            {
                var wfd = tr.Singleton<ObjectWfd2>();
                wfd.C.Type = 2;
                tr.Store(wfd);
                tr.Commit();
            }
            IterateWithApprove();
        }

        public class DuoRefs
        {
            [PrimaryKey]
            public ulong Id { get; set; }
            public Rule1 R1 { get; set; }
            public Rule1 R2 { get; set; }
        }

        public interface IDuoRefsRelation
        {
            void Insert(DuoRefs value);
        }

        [Fact]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void DoubleRefsRelation()
        {
            using (var tr = _db.StartTransaction())
            {
                var creator = tr.InitRelation<IDuoRefsRelation>("DoubleRefsRelation");
                var duoRefsRelation = creator(tr);
                var value = new DuoRefs { Id = 1, R1 = new Rule1() };
                value.R2 = value.R1;
                duoRefsRelation.Insert(value);
                value.Id = 2;
                value.R2 = new Rule1();
                duoRefsRelation.Insert(value);
                tr.Commit();
            }
            IterateWithApprove();
        }

        public class DuoRule1
        {
            public Rule1 R1 { get; set; }
            public Rule1 R2 { get; set; }
        }

        public class DuoDuoRefs
        {
            [PrimaryKey]
            public ulong Id { get; set; }
            public DuoRule1 R1 { get; set; }
            public DuoRule1 R2 { get; set; }
        }

        public interface IDuoDuoRefsRelation
        {
            void Insert(DuoDuoRefs value);
            DuoDuoRefs FindById(ulong Id);
        }

        [Fact]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void DoubleDoubleRefsRelation()
        {
            using (var tr = _db.StartTransaction())
            {
                var creator = tr.InitRelation<IDuoDuoRefsRelation>("DoubleDoubleRefsRelation");
                var duoRefsRelation = creator(tr);
                var value = new DuoDuoRefs { Id = 1, R1 = new DuoRule1 { R1 = new Rule1(), R2 = new Rule1() }, R2 = new DuoRule1 { R1 = new Rule1(), R2 = new Rule1() } };
                duoRefsRelation.Insert(value);
                value.Id = 2;
                value.R1.R2 = value.R2.R1;
                duoRefsRelation.Insert(value);
                value = duoRefsRelation.FindById(2);
                Assert.NotSame(value.R1.R2, value.R2.R1); // Reference equality in multiple levels does not work and cannot work due to backward compatibility
                tr.Commit();
            }
            IterateWithApprove();
        }

        public void Dispose()
        {
            _db.Dispose();
            _lowDb.Dispose();
        }

        public class WithNullable
        {
            [PrimaryKey(1)]
            public int Id { get; set; }
            public int? FieldInt { get; set; }
            public int? FieldIntEmpty { get; set; }
        }

        public interface IRelationWithNullable
        {
            void Insert(WithNullable value);
        }

        [Fact]
        public void IterateNullableValues()
        {
            using (var tr = _db.StartTransaction())
            {
                var creator = tr.InitRelation<IRelationWithNullable>("IterateNullableValues");
                var table = creator(tr);
                table.Insert(new WithNullable { FieldInt = 10 });
                tr.Commit();
            }
            IterateWithApprove();
        }
    }
}
