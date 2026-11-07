# exchange-divisas-DAW2026
Aplicación Web de Exchange de Divisas con Billetera Virtual - ESAN 


## Despliegue local con Docker

El backend se levanta solo desde este repositorio. El frontend está en un repositorio
independiente: [intercambio_Divisas_Front_Web](https://github.com/csMattias09/intercambio_Divisas_Front_Web).

Requisitos: Docker Desktop y las credenciales de Supabase del proyecto (pedirlas al equipo).

```bash
git clone https://github.com/csMattias09/exchange-divisas-DAW2026.git
cd exchange-divisas-DAW2026
cp .env.example .env
# completar .env con la cadena de conexion real a Supabase (Session pooler, puerto 5432)
docker compose up --build
```

- API: http://localhost:5198/api
- Comprobacion rapida (consulta la base de datos): http://localhost:5198/api/moneda

El documento de la entrega está en `Formato de Proyecto 2026-02.docx`.
