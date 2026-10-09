"""Log to a file beside the database. The packaged app has no console."""

import logging

from atten.paths import data_dir


def setup_logging() -> None:
    log_file = data_dir() / "atten.log"
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s %(message)s",
        handlers=[logging.FileHandler(log_file, encoding="utf-8")],
    )
