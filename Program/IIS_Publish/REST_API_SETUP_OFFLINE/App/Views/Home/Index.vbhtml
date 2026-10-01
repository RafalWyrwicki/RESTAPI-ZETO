@Code
    ViewBag.Title = "REST API"
End Code

<main class="container py-5">
    <section class="mx-auto" style="max-width: 760px;" aria-labelledby="apiTitle">
        <div class="card shadow-sm">
            <div class="card-body p-4">
                <h1 id="apiTitle" class="h2 mb-3">REST API działa</h1>
                <p class="lead mb-4">
                    Aplikacja została poprawnie uruchomiona w IIS/IIS Express.
                </p>

                <dl class="row mb-0">
                    <dt class="col-sm-4">Endpoint</dt>
                    <dd class="col-sm-8"><code>POST /api/dane</code></dd>

                    <dt class="col-sm-4">Format</dt>
                    <dd class="col-sm-8"><code>application/json</code></dd>

                    <dt class="col-sm-4">Autoryzacja</dt>
                    <dd class="col-sm-8"><code>Authorization: Bearer &lt;TOKEN_API&gt;</code></dd>

                    <dt class="col-sm-4">Drukowanie</dt>
                    <dd class="col-sm-8">ZPL + RFID EPC 256-bit przez TCP/9100</dd>
                </dl>

                <p class="text-muted mt-4 mb-0">
                    Otwarcie endpointu API bezpośrednio w przeglądarce nie wykonuje
                    żądania POST i nie jest testem integracji.
                </p>
            </div>
        </div>
    </section>
</main>
