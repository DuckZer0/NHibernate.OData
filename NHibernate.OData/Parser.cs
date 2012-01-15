﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NHibernate.OData
{
    internal abstract class Parser
    {
        private readonly IList<Token> _tokens;
        private int _offset;

        protected Parser(string source)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            _tokens = new Lexer(source).ToList();

            if (_tokens.Count == 0)
                throw new ODataException(ErrorMessages.Parser_EmptySource);

            Count = _tokens.Count;
        }

        protected Token Current
        {
            get { return _tokens[_offset]; }
        }

        protected object CurrentLiteral
        {
            get
            {
                var literal = Current as LiteralToken;

                if (literal == null)
                    throw new ODataException(ErrorMessages.Parser_ExpectedLiteral);

                return literal.Value;
            }
        }

        protected string CurrentIdentifier
        {
            get
            {
                var identifier = Current as IdentifierToken;

                if (identifier == null)
                    throw new ODataException(ErrorMessages.Parser_ExpectedIdentifier);

                return identifier.Identifier;
            }
        }

        protected char CurrentSyntax
        {
            get
            {
                var syntax = Current as SyntaxToken;

                if (syntax == null)
                    throw new ODataException(ErrorMessages.Parser_ExpectedSyntax);

                return syntax.Syntax;
            }
        }

        protected int Count { get; private set; }

        protected bool AtStart
        {
            get { return _offset == 0; }
        }

        protected bool AtEnd
        {
            get { return _offset >= Count; }
        }

        protected bool AtPartialEnd
        {
            get { return AtEnd || Current == SyntaxToken.ParenClose; }
        }

        protected Token Previous
        {
            get { return AtStart ? null : _tokens[_offset - 1]; }
        }

        protected Token Next
        {
            get { return _offset < Count - 1 ? _tokens[_offset + 1] : null; }
        }

        protected void MoveNext()
        {
            //if (AtEnd)
            //    throw new ODataException(ErrorMessages.Parser_UnexpectedEnd);

            _offset++;
        }

        protected void Expect(Token token)
        {
            if (token == null)
                throw new ArgumentNullException("token");

            if (AtEnd || !Equals(Current, token))
                throw new ODataException(String.Format(ErrorMessages.Parser_ExpectedToken, token));

            MoveNext();
        }

        public abstract Expression Parse();

        protected void ExpectAtEnd()
        {
            if (!AtEnd)
                throw new ODataException(ErrorMessages.Parser_TrailingTokens);
        }

        protected void ExpectAny()
        {
            if (AtEnd)
                throw new ODataException(ErrorMessages.Parser_UnexpectedEnd);
        }

        protected Expression ParseBool()
        {
            Operator keyword;

            switch (Current.Type)
            {
                case TokenType.Literal:
                    object value = CurrentLiteral;

                    MoveNext();

                    if (AtPartialEnd)
                        return CreateBoolLiteral(value);

                    keyword = GetKeyword(Current);

                    MoveNext();

                    ExpectAny();

                    if (IsLogical(keyword))
                    {
                        return new BoolExpression(
                            keyword,
                            CreateBoolLiteral(value),
                            ParseBool()
                        );
                    }
                    else if (IsCompare(keyword))
                    {
                        return new BoolExpression(
                            keyword,
                            new LiteralExpression(LiteralType.Normal, value),
                            ParseCommon()
                        );
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }

                case TokenType.Syntax:
                    if (Current == SyntaxToken.Negative)
                    {
                        MoveNext();

                        return new ArithmicUnaryExpression(Operator.Negative, ParseCommon());
                    }
                    if (Current == SyntaxToken.ParenOpen)
                    {
                        MoveNext();

                        ExpectAny();

                        var result = new BoolParenExpression(ParseBool());

                        Expect(SyntaxToken.ParenClose);

                        return result;
                    }
                    else
                    {
                        throw new ODataException(ErrorMessages.Parser_ExpectedSyntax);
                    }

                case TokenType.Identifier:
                    if (Next == SyntaxToken.ParenOpen)
                    {
                        var methodCall = ParseMethodCall();

                        MoveNext();

                        if (AtPartialEnd)
                        {
                            if (!methodCall.IsBool)
                                throw new ODataException(ErrorMessages.Parser_ExpectedBooleanExpression);

                            return methodCall;
                        }

                        keyword = GetKeyword(Current);

                        if (IsLogical(keyword))
                        {
                            if (!methodCall.IsBool)
                                throw new ODataException(ErrorMessages.Parser_ExpectedBooleanExpression);

                            return new BoolExpression(
                                keyword,
                                methodCall,
                                ParseBool()
                            );
                        }
                        else if (IsCompare(keyword))
                        {
                            return new BoolExpression(
                                keyword,
                                methodCall,
                                ParseCommon()
                            );
                        }
                        else
                        {
                            return new ArithmicExpression(
                                keyword,
                                methodCall,
                                ParseCommon()
                            );
                        }
                    }
                    else if (CurrentIdentifier == "not")
                    {
                        MoveNext();

                        return new BoolUnaryExpression(Operator.Not, ParseBool());
                    }
                    else
                    {
                        var members = new List<string>();

                        members.Add(CurrentIdentifier);

                        MoveNext();

                        while (Current == SyntaxToken.Slash)
                        {
                            MoveNext();

                            members.Add(CurrentIdentifier);

                            MoveNext();
                        }

                        if (AtPartialEnd)
                            return new MemberExpression(MemberType.Boolean, members);

                        keyword = GetKeyword(Current);

                        if (IsLogical(keyword) || IsCompare(keyword))
                        {
                            return new BoolExpression(
                                keyword,
                                new MemberExpression(MemberType.Normal, members),
                                ParseCommon()
                            );
                        }
                        else
                        {
                            return new ArithmicExpression(
                                keyword,
                                new MemberExpression(MemberType.Normal, members),
                                ParseCommon()
                            );
                        }
                    }
            }

            throw new NotImplementedException();
        }

        private LiteralExpression CreateBoolLiteral(object value)
        {
            if (value is bool)
            {
                return new LiteralExpression(LiteralType.Boolean, value);
            }
            else
            {
                if (value is int)
                {
                    switch ((int)value)
                    {
                        case 0:
                            return new LiteralExpression(LiteralType.Boolean, false);

                        case 1:
                            return new LiteralExpression(LiteralType.Boolean, true);
                    }
                }
            }

            throw new ODataException(ErrorMessages.Parser_ExpectedBooleanLiteral);
        }

        private bool IsLogical(Operator keyword)
        {
            switch (keyword)
            {
                case Operator.And:
                case Operator.Or:
                    return true;

                default:
                    return false;
            }
        }

        private bool IsCompare(Operator keyword)
        {
            switch (keyword)
            {
                case Operator.Eq:
                case Operator.Ge:
                case Operator.Gt:
                case Operator.Le:
                case Operator.Lt:
                case Operator.Ne:
                    return true;

                default:
                    return false;
            }
        }

        private bool IsArithmic(Operator keyword)
        {
            switch (keyword)
            {
                case Operator.Add:
                case Operator.Div:
                case Operator.Mod:
                case Operator.Mul:
                case Operator.Sub:
                    return true;

                default:
                    return false;
            }
        }

        private Operator GetKeyword(Token token)
        {
            var identifier = token as IdentifierToken;

            if (identifier != null)
            {
                switch (identifier.Identifier)
                {
                    case "and": return Operator.And;
                    case "or": return Operator.Or;
                    case "eq": return Operator.Eq;
                    case "ne": return Operator.Ne;
                    case "lt": return Operator.Lt;
                    case "le": return Operator.Le;
                    case "gt": return Operator.Gt;
                    case "ge": return Operator.Ge;
                    case "add": return Operator.Add;
                    case "sub": return Operator.Sub;
                    case "mul": return Operator.Mul;
                    case "div": return Operator.Div;
                    case "mod": return Operator.Mod;
                }
            }

            throw new ODataException(ErrorMessages.Parser_ExpectedKeyword);
        }

        private MethodCallExpression ParseMethodCall()
        {
            throw new NotImplementedException();
        }

        private Expression ParseCommon()
        {
            throw new NotImplementedException();
        }
    }
}