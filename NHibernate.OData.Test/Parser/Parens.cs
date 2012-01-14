﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace NHibernate.OData.Test.Parser
{
    [TestFixture]
    internal class Parens : ParserTestFixture
    {
        [Test]
        public void BoolParen()
        {
            Verify("(true)", new BoolParenExpression(new LiteralExpression(LiteralType.Boolean, true)));
        }

        [Test]
        public void NestedBoolParen()
        {
            Verify("((true))", new BoolParenExpression(new BoolParenExpression(new LiteralExpression(LiteralType.Boolean, true))));
        }

        [Test]
        public void NoOpenParen()
        {
            VerifyThrows("true)");
        }

        [Test]
        public void NoCloseParen()
        {
            VerifyThrows("(true");
        }

        [Test]
        public void OnlyOpenParen()
        {
            VerifyThrows("(");
        }

        [Test]
        public void OnlyCloseParen()
        {
            VerifyThrows(")");
        }
    }
}
