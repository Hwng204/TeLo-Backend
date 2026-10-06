using Infrastructure.Security;

namespace Application.Validators;

public static class LoginValidator
{
    public static bool IsValid(LoginRequest request)
    {
        return request is not null && 
               !string.IsNullOrWhiteSpace(request.Username) && 
               !string.IsNullOrWhiteSpace(request.Password);
    }
}
