using Microsoft.AspNetCore.Identity;

namespace Loja.Customers.Infrastructure;

/// <summary>
/// Identity's validation messages in Brazilian Portuguese (the defaults are in English).
/// Only the errors a shopper can trigger in this store are translated.
/// </summary>
internal sealed class PortugueseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = "Já existe uma conta com este e-mail." };

    // The user name is the email, so a duplicate user name is a duplicate email for the shopper.
    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = "Já existe uma conta com este e-mail." };

    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = "O e-mail informado é inválido." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "O e-mail informado é inválido." };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"A senha deve ter pelo menos {length} caracteres." };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"A senha deve ter pelo menos {uniqueChars} caracteres diferentes." };

    public override IdentityError DefaultError() =>
        new() { Code = nameof(DefaultError), Description = "Ocorreu um erro. Tente novamente." };
}
