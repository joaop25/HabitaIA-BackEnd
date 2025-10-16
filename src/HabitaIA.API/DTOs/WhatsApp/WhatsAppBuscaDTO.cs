namespace HabitaIA.API.DTOs.WhatsApp
{
    public record WhatsAppBuscaDTO(
    string message,   // texto livre recebido do usuário
    int? limite = null  // opcional; default interno = 5
);
}
