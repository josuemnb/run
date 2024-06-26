﻿using System;
using System.Collections.Generic;
using System.IO;

namespace Run {
    public enum AccessModifier {
        PUBLIC,
        PRIVATE,
        PROTECTED,
        INTERNAL,
    }

    public enum AccessType {
        INSTANCE,
        STATIC,
    }
    public class AST {
        internal int Level = 0;
        internal Token Token;
        internal Scanner Scanner;
        internal AST Parent;
        internal Program Program;
        internal Module Module;
        internal string Real;
        internal bool Validated;
        internal bool IsNative;

        internal static AccessModifier CurrentModifier = AccessModifier.PUBLIC;
        internal static AccessType CurrentAccess = AccessType.INSTANCE;

        //public List<Generic> Generics;
        internal List<Annotation> Annotations;
        //public bool IsNullable { get; internal set; }
        internal AccessModifier Modifier { get; set; }
        internal AccessType Access { get; set; }

        //public bool HasGenerics {
        //    get => (Generics != null && Generics.Count > 0);
        //}

        internal static readonly AST NewLine = new();
        internal static readonly AST Empty = new() { Token = Token.Empty };

        public virtual void Parse() {
        }

        public void SetAccess() {
            if (CurrentAccess == AccessType.STATIC && (this is Module)) {
                Program.AddError(Scanner.Current, Error.InvalidAccessDefinition);
                return;
            }
            Modifier = CurrentModifier;
            Access = CurrentAccess;
            CurrentModifier = AccessModifier.PUBLIC;
            CurrentAccess = AccessType.INSTANCE;
        }

        public void SetParent(AST parent) {
            Parent = parent;
            Module = parent.Module;
            Program = parent.Program;
            Scanner = parent.Scanner;
            Token ??= Scanner.Current;
            Level = parent.Level + 1;
        }

        public AST GetRoot() {
            var temp = this;
            while (temp.Parent != null) {
                temp = temp.Parent;
            }
            return temp;
        }

        public virtual AST FindName(string name) {
            if (Token != null && (Token.Value == name || Real == name) && this is not Expression) return this;
            return Parent != this ? Parent?.FindName(name) : null;
        }

        public virtual AST FindChildren(string name) {
            if (Token != null && Token.Value == name) { return this; }
            return null;
        }

        public T FindParent<T>() {
            var temp = this;
            while (temp != null) {
                if (temp is T t) return t;
                if (temp == temp.Parent) return default;
                temp = temp.Parent;
            }
            return default;
        }

        public bool GetName(out Token token) {
            token = Scanner.GetName(false);
            if (token == null) {
                Program.AddError(Scanner.Current, Error.ExpectingName);
                return false;
            }
            return true;
        }

        public virtual void GetAnnotations() {
            Annotations = new(0);

            if (Parent is not Block block) return;
            for (int i = block.Children.Count - 2; i >= 0; i--) {
                var child = block.Children[i];
                if (child is not Annotation a) break;
                block.Children.RemoveAt(i);
                Annotations.Add(a);
                a.SetParent(this);
                switch (a.Token.Value) {
                    case "primitive": {
                            if (this is Class cls) cls.IsPrimitive = true;
                        }
                        break;
                    case "native": {
                            if (this is Class cls) {
                                cls.IsNative = true;
                                cls.NativeName = a.Value;
                                break;
                            }
                            if (this is Function fun) {
                                fun.IsNative = true;
                                if (a.Value != null) {
                                    var start = a.Value.IndexOf('(');
                                    var end = a.Value.IndexOf(')');
                                    if (start > -1 && end > -1) {
                                        fun.NativeNames = [
                                            a.Value[..start],
                                            a.Value.Substring(start + 1, end - start - 1)
                                        ];
                                    } else {
                                        fun.NativeNames = [a.Value];
                                    }
                                }
                            }
                        }
                        break;
                }
            }
        }

        public virtual void Print() => Print(this);

        public static void Print(AST ast) {
            if (ast == null) return;
            Console.WriteLine(new string(' ', ast.Level * 2) + "[" + ast.GetType().Name + "] " + ast.Token?.Value);
        }

        public static List<Generic> ParseGenerics(AST ast) {
            var generics = new List<Generic>(0);
        again:
            var gen = new Generic();
            gen.SetParent(ast);
            gen.Parse();

            generics.Add(gen);
            if (ast.Scanner.Expect(',')) goto again;
            if (ast.Scanner.Expect('>') == false) {
                ast.Program.AddError(ast.Scanner.Current, "Expecting >");
            }
            return generics;
        }

        //public virtual void Save(TextWriter writer, Builder builder) {
        //    writer.Write(Real ?? Token?.Value);
        //}

        public IEnumerable<T> DeepFindChildrenInternal<T>() where T : AST {
            if (this is T t1) yield return t1;
            if (this is GetterSetter gt) {
                if (gt.Getter != null) {
                    foreach (var r in gt.Getter.DeepFindChildrenInternal<T>()) {
                        yield return r;
                    }
                }
                if (gt.Setter != null) {
                    foreach (var r in gt.Setter.DeepFindChildrenInternal<T>()) {
                        yield return r;
                    }
                }
            }
            if (this is Var v && v.Initializer is Expression init) {
                foreach (var r in ExpressionHelper.FindChildren<T>(init)) {
                    yield return r;
                }
            } else if (this is Expression exp) {
                foreach (var r in ExpressionHelper.FindChildren<T>(exp)) {
                    yield return r;
                }
            } else if (this is Block b) {
                for (int i = 0; i < b.Children.Count; i++) {
                    foreach (var r in b.Children[i].DeepFindChildrenInternal<T>()) {
                        yield return r;
                    }
                }
            }
        }
    }
}
