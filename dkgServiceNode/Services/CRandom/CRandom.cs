// Copyright (C) 2024 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of dkg service node
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.


namespace dkgServiceNode.Services.CRandom;

public static class CR
{
    public static int? Calculate(string publicKey)
    {
        int? IntValue = null;
        try
        {
            byte[] decodedBytes = Convert.FromBase64String(publicKey);
            decodedBytes[0] = 0;
            IntValue = Calculate(decodedBytes);
        }
        catch { }
        return IntValue;
    }

    public static int? Calculate(byte[] decodedBytes)
    {
        int? IntValue = null;
        try
        {
            decodedBytes[0] = 0;
            if (BitConverter.IsLittleEndian)
            {
                Array.Resize(ref decodedBytes, 4);
                decodedBytes = decodedBytes.Reverse().ToArray();
            }
            IntValue = BitConverter.ToInt32(decodedBytes, 0);
        }
        catch { }
        return IntValue;
    }

}

