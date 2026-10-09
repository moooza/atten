from atten.persian import english_digits, normalize_text, persian_letters


def test_arabic_yeh_and_kaf_become_persian():
    assert persian_letters("عل\u064a") == "عل\u06cc"
    assert persian_letters("\u0643اظم\u0649") == "\u06a9اظم\u06cc"
    assert persian_letters("\u06a9\u062a\u0627\u0628") == "\u06a9\u062a\u0627\u0628"


def test_persian_and_arabic_digits_become_english():
    assert english_digits("۰۹۱۲") == "0912"
    assert english_digits("\u0661\u0662\u0663") == "123"
    assert english_digits("ساعت ۸") == "ساعت 8"


def test_normalize_text_fixes_letters_and_digits_together():
    assert normalize_text("\u06a9\u062f \u064a۱۲") == "\u06a9\u062f \u06cc12"
