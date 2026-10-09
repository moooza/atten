"""Store Persian yeh and kaf, not the Arabic letters keyboards sometimes emit."""

_ARABIC_TO_PERSIAN = str.maketrans(
    {
        "\u064a": "\u06cc",  # ي → ی
        "\u0649": "\u06cc",  # ى → ی
        "\u0643": "\u06a9",  # ك → ک
    }
)


def persian_letters(value: str) -> str:
    return value.translate(_ARABIC_TO_PERSIAN)
