using System;
using Buddie.Security;

namespace Buddie.Tests
{
    class ApiKeyProtectionTest
    {
        static void Main()
        {
            Console.WriteLine("=== API Key Protection Test ===\n");

            // Test 1: Encryption and Decryption
            Console.WriteLine("Test 1: Encryption and Decryption");
            string originalKey = "sk-1234567890abcdefghijklmnopqrstuvwxyz";
            Console.WriteLine($"Original Key: {originalKey}");

            string encrypted = ApiKeyProtection.Protect(originalKey);
            Console.WriteLine($"Encrypted: {encrypted}");

            string decrypted = ApiKeyProtection.Unprotect(encrypted);
            Console.WriteLine($"Decrypted: {decrypted}");
            Console.WriteLine($"Match: {originalKey == decrypted}\n");

            // Test 2: Masking for display
            Console.WriteLine("Test 2: Masking for Display");
            Console.WriteLine($"Original: {originalKey}");
            Console.WriteLine($"Masked: {ApiKeyProtection.Mask(originalKey)}\n");

            // Test 3: Deep masking for export
            Console.WriteLine("Test 3: Deep Masking for Export");
            Console.WriteLine($"Original: {originalKey}");
            Console.WriteLine($"Deep Masked: {ApiKeyProtection.DeepMask(originalKey)}\n");

            // Test 4: Check if protected
            Console.WriteLine("Test 4: Check if Protected");
            Console.WriteLine($"Is '{originalKey}' protected? {ApiKeyProtection.IsProtected(originalKey)}");
            Console.WriteLine($"Is '{encrypted}' protected? {ApiKeyProtection.IsProtected(encrypted)}\n");

            // Test 5: Backward compatibility (handling unencrypted keys)
            Console.WriteLine("Test 5: Backward Compatibility");
            string unencryptedKey = "plain-api-key-12345";
            Console.WriteLine($"Unencrypted Key: {unencryptedKey}");
            Console.WriteLine($"Unprotect unencrypted: {ApiKeyProtection.Unprotect(unencryptedKey)}");
            Console.WriteLine($"Should return same: {unencryptedKey == ApiKeyProtection.Unprotect(unencryptedKey)}\n");

            // Test 6: Different length keys
            Console.WriteLine("Test 6: Different Length Keys");
            string[] testKeys = { "ab", "abcd", "abcdefgh", "abcdefghijklmnop", "abcdefghijklmnopqrstuvwxyz1234567890" };
            foreach (var key in testKeys)
            {
                Console.WriteLine($"Key (len={key.Length}): {key} -> Masked: {ApiKeyProtection.Mask(key)}");
            }

            Console.WriteLine("\n=== Test Complete ===");
        }
    }
}