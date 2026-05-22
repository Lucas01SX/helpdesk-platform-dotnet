namespace Helpdesk.Modules.Identity.Application.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);

    // Returns a pre-computed hash in the same format as Hash() output.
    // Used by LoginUseCase to run the full KDF even when the email doesn't exist,
    // preventing timing-based email enumeration.
    string GetDummyHash();
}
