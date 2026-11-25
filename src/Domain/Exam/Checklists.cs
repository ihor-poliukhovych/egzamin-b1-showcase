namespace B1.Domain.Exam;

public static class Checklists
{
    public static readonly IReadOnlyDictionary<string, string[]> Writing = new Dictionary<string, string[]>
    {
        ["zyczenia"] = ["Miejsce i data", "Zwrot do adresata (wołacz + !)", "Okazja i tekst życzeń, wyodrębniony", "Podpis z prawej pod tekstem", "Styl dopasowany (oficjalny / prywatny)"],
        ["pozdrowienia"] = ["Miejsce i data", "Zwrot do adresata (wołacz + !)", "Treść pozdrowień", "Podpis z prawej pod tekstem", "Styl dopasowany (oficjalny / prywatny)"],
        ["ogloszenie"] =["Kto ogłasza", "W jakim celu (sprzedaje, szuka…)", "Co jest przedmiotem", "Jak się skontaktować (tel., adres)", "Zwięzłość"],
        ["zaproszenie"] = ["Kto i kogo zaprasza", "Z jakiej okazji", "Gdzie i kiedy", "Prośba o potwierdzenie / strój (opcjonalnie)", "Podpis i styl"],
        ["sprawozdanie"] = ["Czas, miejsce, okoliczności, cel", "Przebieg chronologicznie, czas przeszły", "Ocena wydarzeń", "Wstęp, rozwinięcie, zakończenie"],
        ["charakterystyka"] = ["Dane: imię, wiek, zawód", "Wygląd", "Cechy charakteru (+/−)", "Zainteresowania", "Ocena postaci"],
        ["opowiadanie"] = ["Wstęp → rozwinięcie → zakończenie", "Czas przeszły", "Logiczna kolejność, własny pomysł", "1. lub 3. osoba", "Zdanie początkowe z polecenia (jeśli podane)"],
        ["esej"] = ["Wstęp → rozwinięcie → zakończenie", "Subiektywne poglądy i argumenty", "Zwroty: Moim zdaniem… / Na zakończenie…"],
        ["list"] = ["Miejsce i data (prawy górny róg)", "Zwrot grzecznościowy (wołacz + !)", "Wstęp (cel listu) → rozwinięcie → zakończenie", "Formuła kończąca", "Podpis"],
        ["opinia"] = ["Wstęp: o czym (miejsce, film, serial)", "Opis / krótkie streszczenie", "Ocena subiektywna z uzasadnieniem (+/−)", "Polecam / nie polecam — zakończenie", "Styl dopasowany do portalu / gazetki"],
        ["opis"] = ["Wstęp: co opisuję, gdzie, dlaczego", "Elementy w ustalonej kolejności (od ogółu do szczegółu)", "Przymiotniki: wielkość, kształt, kolor, wygląd", "Zakończenie: ogólne wrażenie / ocena", "Wstęp → rozwinięcie → zakończenie"],
        ["sms"] = ["Zwrot do adresata", "Cel wiadomości na początku", "Wszystkie punkty polecenia (pytania, propozycja spotkania)", "Podpis", "Zwięzłość, bez miejsca i daty"],
    };

    public static readonly IReadOnlyDictionary<string, string[]> Speaking = new Dictionary<string, string[]>
    {
        ["opis"] = ["Osoby: wygląd, ubiór, emocje, relacje", "Miejsce i sytuacja: plany, kolory, pogoda, nastrój", "Czynności: co robi każda osoba", "Kolejność od ogółu do szczegółu"],
        ["monolog"] = ["Wstęp (informacje wstępne)", "Co najmniej 2 aspekty rozwinięcia", "Zakończenie / podsumowanie", "Łączniki (na początku, po pierwsze… na koniec)", "Wszystkie punkty polecenia"],
        ["sytuacja"] = ["Rozmowa rozpoczęta i zakończona", "Rejestr właściwy (Pan/Pani vs ty)", "Funkcje z polecenia wykonane (zapytał / przekonał / umówił)", "Reakcje na rozmówcę, podtrzymanie rozmowy"],
    };
}
