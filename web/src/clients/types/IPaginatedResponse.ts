export interface IPaginatedResponse<T> {
    statusCode: number;
    data: T[];
    size: number;
    page: number;
    pages: number;
}