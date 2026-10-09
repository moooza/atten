"""Normalize typed Persian before it is stored.

Windows Persian keyboards often emit Arabic yeh and kaf. Stored text uses the
Persian letters, and stored digits are ASCII.
"""

_ARABIC_TO_PERSIAN = str.maketrans(
    {
        "\u064a": "\u06cc",  # ي → ی
        "\u0649": "\u06cc",  # ى → ی
        "\u0643": "\u06a9",  # ك → ک
    }
)
_DIGITS = str.maketrans("۰۱۲۳۴۵۶۷۸۹٠١٢٣٤٥٦٧٨٩", "01234567890123456789")


def persian_letters(value: str) -> str:
    return value.translate(_ARABIC_TO_PERSIAN)


def english_digits(value: str) -> str:
    return value.translate(_DIGITS)


def normalize_text(value: str) -> str:
    return english_digits(persian_letters(value))
