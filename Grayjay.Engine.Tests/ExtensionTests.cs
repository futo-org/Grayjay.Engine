using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grayjay.Engine.Tests
{

    [TestClass]
    public class ExtensionTests
    {


        [TestMethod]
        public void Match_Domain_Equivelant()
        {
            Assert.IsTrue("domain.com".MatchesDomain("domain.com"));
            Assert.IsTrue("sub.domain.com".MatchesDomain("sub.domain.com"));
        }
        [TestMethod]
        public void Match_Domain_Wildcard()
        {
            Assert.IsTrue("domain.com".MatchesDomain(".domain.com"));
            Assert.IsTrue("sub.domain.com".MatchesDomain(".domain.com"));
            Assert.IsTrue("domain.com.au".MatchesDomain(".domain.com.au"));
            Assert.IsTrue("sub.domain.com.au".MatchesDomain(".domain.com.au"));
        }


        [TestMethod]
        public void Match_Domain_FLD_Wildcard_Fail()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                Assert.IsFalse("domain.com".MatchesDomain(".com"));
            });
        }
        [TestMethod]
        public void Match_Domain_SLD_Wildcard_Fail()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                Assert.IsFalse("domain.com.au".MatchesDomain(".com.au"));
            });
        }
        [TestMethod]
        public void Match_Domain_TLD_Wildcard_Fail()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                Assert.IsFalse("domain.wcape.school.za".MatchesDomain(".wcape.school.za"));
            });
        }
    }
}
