# Postman collection

`halisaha.postman_collection.json` API'nin uçtan uca akışını ve auth sözleşmesinin hata durumlarını sırayla dener:

| # | İstek | Beklenen |
|---|---|---|
| 1 | Register | 201 + `userId` (her çalıştırmada yeni e-posta) |
| 2 | Register, aynı e-posta | 409 `auth.email_taken` |
| 3 | Login | 200 `{ userId, accessToken, expiresIn }`; token `accessToken` değişkenine yazılır |
| 4 | Login, yanlış parola | 401 `auth.invalid_credentials`, `WWW-Authenticate: Bearer` |
| 5 | Me | 200, kendi bilgilerin ve profilin |
| 6 | Me, token yok | 401 `auth.unauthenticated`, `WWW-Authenticate: Bearer` |
| 7 | Me, geçersiz token | 401 `auth.unauthenticated`, `WWW-Authenticate: Bearer error="invalid_token"` |

Otomatik testlerin yerine geçmez (onlar `tests/` altında); demo, sunum ve elle deneme içindir.

## Kullanım

1. API'yi çalıştır: `dotnet run --project src/Halisaha.Api --launch-profile http` (adres `http://localhost:5243`).
2. Postman → Import → bu klasördeki iki dosyayı seç.
3. Sağ üstten **Halısaha local** environment'ını seç.
4. Collection'a sağ tıkla → **Run collection**. Sıra önemli: 3. istek token'ı yazar, 5. istek onu kullanır.

Komut satırından (Newman):

```
npx newman run docs/postman/halisaha.postman_collection.json -e docs/postman/halisaha-local.postman_environment.json
```

## Veri

- Her çalıştırma `newman+<guid>@test.local` biçiminde yeni bir kullanıcı oluşturur; aynı veritabanında tekrar
  tekrar çalıştırılabilir, çakışma (409) olmaz. 2. istekteki 409 bilerek üretilir.
- İlçe: `districtId` = 15009 (Burdur / Merkez), seed verisinden. Environment'tan değiştirilebilir.
- Development veritabanında çalıştırırsan test kullanıcıları orada birikir; zararsızdır. Temizlemek istersen
  yalnızca bu collection'ın oluşturduğu kullanıcıları siler (profilleri cascade ile gider):

```sql
DELETE FROM "AspNetUsers" WHERE "Email" LIKE 'newman+%@test.local';
```

- 4. istek her çalıştırmada yeni kullanıcı için tek bir yanlış deneme yapar; hesap kilidine (5 deneme) ulaşmaz.
