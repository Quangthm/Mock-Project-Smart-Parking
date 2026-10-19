import math
import os
import re
from datetime import datetime, timezone
from uuid import UUID

import psycopg


class RecognitionStoreError(RuntimeError):
    pass


def database_connection_info():
    database_url = os.environ.get("IOT_DATABASE_URL")
    if database_url:
        return database_url

    password = os.environ.get("SMARTPARK_DEV_PASSWORD")
    if not password or password.startswith("replace-with-"):
        raise RecognitionStoreError(
            "Set SMARTPARK_DEV_PASSWORD in the repository .env or IOT_DATABASE_URL "
            "before starting the IoT AI service."
        )

    return {
        "host": os.environ.get("IOT_DATABASE_HOST", "localhost"),
        "port": int(os.environ.get("SMARTPARK_DB_IOT_PORT", "5446")),
        "dbname": os.environ.get("IOT_DATABASE_NAME", "smartpark_iot"),
        "user": os.environ.get("IOT_DATABASE_USER", "smartpark_iot"),
        "password": password,
        "connect_timeout": 5,
    }


def open_connection():
    connection_info = database_connection_info()
    if isinstance(connection_info, str):
        return psycopg.connect(connection_info)
    return psycopg.connect(**connection_info)


def validate_store_configuration():
    try:
        with open_connection() as connection:
            connection.execute("SELECT 1 FROM devices LIMIT 0")
            connection.execute("SELECT 1 FROM lpr_events LIMIT 0")
    except Exception as error:
        raise RecognitionStoreError(
            f"Could not connect to the IoT database or find the devices and "
            f"lpr_events tables: {error}"
        ) from error


def require_event_context(data):
    try:
        tenant_id = str(UUID(str(data.get("tenant_id", ""))))
        site_id = str(UUID(str(data.get("site_id", ""))))
    except (TypeError, ValueError, AttributeError) as error:
        raise ValueError("tenant_id and site_id must be valid UUIDs.") from error

    direction = str(data.get("direction", "")).upper()
    if direction not in ("INBOUND", "OUTBOUND"):
        raise ValueError("direction must be INBOUND or OUTBOUND.")
    return tenant_id, site_id, direction


def persist_recognition_event(
    tenant_id,
    site_id,
    direction,
    plate,
    confidence,
):
    original_plate = str(plate).strip()
    normalized_plate = re.sub(r"[^A-Z0-9]", "", original_plate.upper())
    if not normalized_plate:
        return

    confidence_score = None
    if confidence is not None:
        confidence_value = float(confidence)
        if not math.isfinite(confidence_value):
            raise RecognitionStoreError("AI confidence must be a finite number.")
        confidence_score = min(100.0, max(0.0, confidence_value * 100 if confidence_value <= 1 else confidence_value))

    captured_at = datetime.now(timezone.utc)
    lock_key = f"{tenant_id}:{site_id}:{direction}:{normalized_plate}"
    try:
        with open_connection() as connection:
            connection.execute(
                "SELECT pg_advisory_xact_lock(hashtextextended(%s, 0))",
                (lock_key,),
            )
            recent = connection.execute(
                """
                SELECT 1
                FROM lpr_events
                WHERE tenant_id = %s AND site_id = %s
                  AND direction = %s AND normalized_plate = %s
                  AND captured_at > now() - INTERVAL '8 seconds'
                LIMIT 1
                """,
                (tenant_id, site_id, direction, normalized_plate),
            ).fetchone()
            if recent:
                return

            device_id = connection.execute(
                """
                INSERT INTO devices
                    (tenant_id, site_id, device_code, device_type, status,
                     location_desc, last_ping_at)
                VALUES (%s, %s, 'OPERATOR_AI_SCANNER', 'CAMERA_LPR', 'ONLINE',
                        'Operator AI recognition service', %s)
                ON CONFLICT (tenant_id, site_id, device_code)
                DO UPDATE SET last_ping_at = EXCLUDED.last_ping_at,
                              updated_at = CURRENT_TIMESTAMP
                RETURNING id
                """,
                (tenant_id, site_id, captured_at),
            ).fetchone()[0]

            connection.execute(
                """
                INSERT INTO lpr_events
                    (device_id, tenant_id, site_id, original_plate, normalized_plate,
                     confidence_score, direction, processed_status, captured_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, 'UNPROCESSED', %s)
                """,
                (
                    device_id,
                    tenant_id,
                    site_id,
                    original_plate,
                    normalized_plate,
                    confidence_score,
                    direction,
                    captured_at,
                ),
            )
    except Exception as error:
        raise RecognitionStoreError(f"Could not persist AI recognition event: {error}") from error
