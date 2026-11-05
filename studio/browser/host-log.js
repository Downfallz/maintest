// Admin is printed after every seat and session line. Wait for that complete line rather than taking a
// snapshot of stdout as soon as the first invitation appears; OS pipe chunks need not end on a line.
export function hostLinks(log) {
  const admin = /Admin: (\S+)[^\S\r\n]*\r?\n/.exec(log)?.[1];
  const joins = [...log.matchAll(/joins at (\S+)[^\S\r\n]*\r?\n/g)].map(match => match[1]);
  const session = /Session (\S+)[^\r\n]*\r?\n/.exec(log)?.[1];
  return admin && joins.length && session ? { admin, joins, join: joins[0], session } : null;
}
