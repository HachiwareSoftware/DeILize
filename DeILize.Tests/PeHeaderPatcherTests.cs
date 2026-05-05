using NUnit.Framework;
using System;
using System.IO;
using DeILize.Runtime;

namespace DeILize.Tests
{
    [TestFixture]
    public class PeHeaderPatcherTests
    {
        private byte[] _pe32Assembly;
        private byte[] _pe32PlusAssembly;

        [SetUp]
        public void Setup()
        {
            _pe32Assembly = CreateFakePeImage(isPE32Plus: false);
            _pe32PlusAssembly = CreateFakePeImage(isPE32Plus: true);
        }

        [Test]
        public void PatchBytes_ValidPE32_ZeroesClrDirectory()
        {
            bool result = PeHeaderPatcher.PatchBytes(_pe32Assembly);

            Assert.That(result, Is.True);

            int dataDirOff = GetDataDirOffset(_pe32Assembly, isPE32Plus: false);
            int clrEntryOff = dataDirOff + 14 * 8;
            int rva = BitConverter.ToInt32(_pe32Assembly, clrEntryOff);
            int size = BitConverter.ToInt32(_pe32Assembly, clrEntryOff + 4);

            Assert.That(rva == 0, Is.True);
            Assert.That(size == 0, Is.True);
        }

        [Test]
        public void PatchBytes_ValidPE32Plus_ZeroesClrDirectory()
        {
            bool result = PeHeaderPatcher.PatchBytes(_pe32PlusAssembly);

            Assert.That(result, Is.True);

            int dataDirOff = GetDataDirOffset(_pe32PlusAssembly, isPE32Plus: true);
            int clrEntryOff = dataDirOff + 14 * 8;
            int rva = BitConverter.ToInt32(_pe32PlusAssembly, clrEntryOff);
            int size = BitConverter.ToInt32(_pe32PlusAssembly, clrEntryOff + 4);

            Assert.That(rva == 0, Is.True);
            Assert.That(size == 0, Is.True);
        }

        [Test]
        public void PatchBytes_InvalidData_ReturnsFalse()
        {
            byte[] invalid = new byte[] { 0, 0, 0, 0 };
            bool result = PeHeaderPatcher.PatchBytes(invalid);
            Assert.That(result, Is.False);
        }

        [Test]
        public void PatchBytes_NoMZ_ReturnsFalse()
        {
            byte[] noMZ = new byte[256];
            noMZ[0] = 0x41;
            bool result = PeHeaderPatcher.PatchBytes(noMZ);
            Assert.That(result, Is.False);
        }

        [Test]
        public void PatchBytes_AlreadyZeroed_StillSucceeds()
        {
            int dataDirOff = GetDataDirOffset(_pe32Assembly, isPE32Plus: false);
            int clrEntryOff = dataDirOff + 14 * 8;
            Array.Clear(_pe32Assembly, clrEntryOff, 8);

            bool result = PeHeaderPatcher.PatchBytes(_pe32Assembly);
            Assert.That(result, Is.True);
        }

        private static int GetDataDirOffset(byte[] pe, bool isPE32Plus)
        {
            int e_lfanew = BitConverter.ToInt32(pe, 0x3C);
            int optionalHeaderOff = e_lfanew + 4 + 20;
            return optionalHeaderOff + (isPE32Plus ? 112 : 96);
        }

        private static byte[] CreateFakePeImage(bool isPE32Plus)
        {
            int optionalHeaderSize = isPE32Plus ? 240 : 224;
            int totalSize = 512 + optionalHeaderSize;

            var pe = new byte[totalSize];

            pe[0] = 0x4D;
            pe[1] = 0x5A;

            int e_lfanew = 0x80;
            Buffer.BlockCopy(BitConverter.GetBytes(e_lfanew), 0, pe, 0x3C, 4);

            int ntHeadersOff = e_lfanew;
            pe[ntHeadersOff] = 0x50;
            pe[ntHeadersOff + 1] = 0x45;
            pe[ntHeadersOff + 2] = 0x00;
            pe[ntHeadersOff + 3] = 0x00;

            int fileHeaderOff = ntHeadersOff + 4;
            pe[fileHeaderOff] = 0x4C;
            pe[fileHeaderOff + 1] = 0x01;

            int optionalHeaderOff = fileHeaderOff + 20;
            pe[optionalHeaderOff] = (byte)(isPE32Plus ? 0x0B : 0x0B);
            pe[optionalHeaderOff + 1] = (byte)(isPE32Plus ? 0x02 : 0x01);

            int dataDirOff = optionalHeaderOff + (isPE32Plus ? 112 : 96);
            int clrEntryOff = dataDirOff + 14 * 8;
            Buffer.BlockCopy(BitConverter.GetBytes(0x2000), 0, pe, clrEntryOff, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(0x100), 0, pe, clrEntryOff + 4, 4);

            return pe;
        }
    }
}
