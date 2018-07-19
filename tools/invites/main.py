import copy
import json
import config
import argparse
import subprocess
import operator
import pymysql

from datetime import datetime
from datetime import timedelta

INVITES_PER_BLOCK = 10

# Parse arguments
ap = argparse.ArgumentParser()
ap.add_argument("-d", "--days", required=True, help="length of period(in days) to collect data about invites")
ap.add_argument("-f", "--filename", required=False, help="Filename to store results (.csv format)",
                default="mempool_invites.csv")
args = vars(ap.parse_args())


def connect_to_mysql():
    """
    Connect to the mempool MYSQL database

    :return: pymysql.Connection
    """

    try:
        conn = pymysql.connect(host=config.DB_MYSQL_HOST, port=config.DB_MYSQL_PORT, user=config.DB_MYSQL_USER,
                               passwd=config.DB_MYSQL_USERPASS, db=config.DB_MYSQL_NAME)
    except pymysql.err.OperationalError as e:
        print("An Error occurred: ", e)
        exit(1)

    return conn


def get_numbers_of_blocks(connection, days):
    """
    Get number of generated and processed blocks for the last n days(argument)

    :param connection: pymysql.Connection
    :param days: number of days to search for

    :return: number of blocks
    :rtype: int
    """

    cur = connection.cursor()
    last_date = datetime.now() - timedelta(days=days)
    last_date = last_date.strftime('%Y-%m-%d %H:%M:%S')

    cur.execute("SELECT COUNT(*) FROM Block "
                "WHERE CreatedAt > '{}' ".format(last_date))

    for row in cur:
        number = row[0]
        cur.close()
        return number


def get_payments(connection, days):
    """
    Get payments for users(for the last n days)

    :param connection: pymysql.Connection
    :param days: number of days to search for
    :return: accelerated object with number payments for person for period(n days)
    :rtype: object
    """

    cur = connection.cursor()
    last_date = datetime.now() - timedelta(days=days)
    last_date = last_date.strftime('%Y-%m-%d %H:%M:%S')

    cur.execute("SELECT AccountId, sum(Amount), COUNT(*) AS 'number_of_payments', Username, Address "
                "FROM Payment "
                "INNER JOIN Account "
                "On AccountId = Account.Id "
                "WHERE Payment.CreatedAt > '{}' ".format(last_date) +
                "GROUP BY AccountId;")

    payments = cur
    cur.close()

    return payments


def get_total_invites_in_mempool():
    """
    Call merit-cli subprocess and get the number of free invites in mempool

    :rtype: int
    """

    invites_in_mempool = json.loads(
        (subprocess.check_output(['merit-cli',
                                  '-conf={}'.format(config.MERIT_CONF),
                                  '-datadir={}'.format(config.MERIT_DATA_DIR),
                                  'getaddressbalance',
                                  '{"addresses":["' + config.MERIT_MEMPOOL_ADDRESS + '"], "invites":true}']))
            .decode("utf-8"))

    return invites_in_mempool['balance']


def get_invites_per_address(payments, number_of_blocks, free_invites_in_mempool):
    """
    Get number of invites per each user

    :param payments: accelerated object with number payments for person for period(n days)
    :param number_of_blocks: generated and processed blocks for the last n days
    :param free_invites_in_mempool: number of free(collected) invites in the mempool
    :return: list[ tuple(address, number_of_invites), ... ]
    """

    available_invites_to_dist = free_invites_in_mempool if (
            free_invites_in_mempool < number_of_blocks * INVITES_PER_BLOCK) else number_of_blocks * INVITES_PER_BLOCK

    copypayments = copy.copy(payments)
    total_payments = sum(payment[1] for payment in copypayments)

    total_invites_to_share = 0
    invites_per_address = {}
    for payment in payments:
        val = round(float(payment[1] / total_payments) * available_invites_to_dist)
        invites_per_address[payment[4]] = val

        total_invites_to_share += val

    invites_per_address = sorted(invites_per_address.items(), key=operator.itemgetter(1), reverse=True)

    # If after rounding we need more invites then there are available, then "cut" top guys
    if total_invites_to_share > available_invites_to_dist:
        # The number of invites that are missing
        need = total_invites_to_share - available_invites_to_dist

        for i in range(need):
            invites_per_address[i][1] -= 1

    return invites_per_address


if __name__ == "__main__":
    mysql_conn = connect_to_mysql()
    payments = get_payments(mysql_conn, int(args["days"]))
    number_of_blocks = get_numbers_of_blocks(mysql_conn, int(args["days"]))
    free_invites_in_mempool = get_total_invites_in_mempool()

    invites_per_address = get_invites_per_address(payments, number_of_blocks, free_invites_in_mempool)

    # Save results to .cvs file
    with open(args["filename"], 'w') as f:
        [f.write('{0},{1}\n'.format(item[0], item[1])) if (item[1] > 0) else '' for item in invites_per_address]

    mysql_conn.close()
