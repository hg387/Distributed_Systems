import numpy
import time
import math
from pyspark.sql import SparkSession
from pyspark.ml.feature import HashingTF, IDF, Tokenizer, StopWordsRemover, CountVectorizer
from pyspark.sql.functions import mean as _mean, stddev as _stddev, col

training_spam_data = {}
training_spam_big_array = numpy.empty([1, 500])
training_spam_df = None

training_ham_data = {}
training_ham_big_array = numpy.empty([1, 500])
training_ham_df = None

testing_spam_data = {}
testing_spam_big_array = numpy.empty([1, 500])
testing_spam_df = None

testing_ham_data = {}
testing_ham_big_array = numpy.empty([1, 500])
testing_ham_df = None


def prep_files(files, training=True, spam=True):
    global spark
    global training_spam_big_array
    global training_ham_big_array
    global testing_spam_big_array
    global testing_ham_big_array

    fnames = []
    fcontents = []
    for f in files:
        fnames.append(f[0])
        content_tmp = f[1].split("\n")
        content = ' '.join(content_tmp)
        fcontents.append([content])

    prep_features(fnames, fcontents, training, spam)

    if (training):
        if (spam):
            training_spam_big_array = training_spam_big_array[1:, :]

        else:
            training_ham_big_array = training_ham_big_array[1:, :]

    else:
        if (spam):
            testing_spam_big_array = testing_spam_big_array[1:, :]

        else:
            testing_ham_big_array = testing_ham_big_array[1:, :]



def prep_features(fnames, fcontents, training=True, spam=True):
    global training_spam_big_array
    global training_ham_big_array
    global testing_spam_big_array
    global testing_ham_big_array

    df_content = spark.createDataFrame(fcontents, ["content"])

    tokenizer = Tokenizer(inputCol="content", outputCol="words")
    wordsData = tokenizer.transform(df_content)

    # wordsData.show(truncate=False)

    remover = StopWordsRemover(inputCol="words", outputCol="filtered")
    updatedWordsData = remover.transform(wordsData)

    # number of features are set to be 500 for now
    hashingTF = HashingTF(inputCol="filtered", outputCol="rawFeatures", numFeatures=500)
    featurizedData = hashingTF.transform(updatedWordsData)

    # cv = CountVectorizer(inputCol="filtered", outputCol="rawFeatures", vocabSize=50)
    # model = cv.fit(updatedWordsData)
    # featurizedData = model.transform(updatedWordsData)

    idf = IDF(inputCol="rawFeatures", outputCol="features")
    idfModel = idf.fit(featurizedData)
    rescaledData = idfModel.transform(featurizedData)

    # rescaledData.select("features").show()

    vectors = rescaledData.select("features").rdd.map(lambda x: x[0]).collect()
    for i in range(0, len(vectors)):
        vector = numpy.array((vectors[i]).toArray())
        vector = vector.reshape(1, 500)
        # print(vector.shape)

        if (training):
            if (spam):
                training_spam_data[fnames[i]] = vector
                training_spam_big_array = numpy.vstack([training_spam_big_array, vector])
            else:
                training_ham_data[fnames[i]] = vector
                training_ham_big_array = numpy.vstack([training_ham_big_array, vector])
        else:
            if (spam):
                testing_spam_data[fnames[i]] = vector
                testing_spam_big_array = numpy.vstack([testing_spam_big_array, vector])
            else:
                testing_ham_data[fnames[i]] = vector
                testing_ham_big_array = numpy.vstack([testing_ham_big_array, vector])


def standardize_data(training=True):
    global training_ham_df
    global training_spam_df
    global testing_ham_df
    global testing_spam_df
    if (training):
        global training_spam_big_array
        global training_ham_big_array
        training_big_array = numpy.concatenate((training_spam_big_array, training_ham_big_array), axis=0)
        training_big_array -= numpy.mean(training_big_array)
        training_big_array /= numpy.std(training_big_array)

        training_spam_big_array = training_big_array[:len(training_spam_big_array), :]
        training_ham_big_array = training_big_array[len(training_spam_big_array):, :]
        # print("training_spam_big_array is", training_spam_big_array)
        # print("training_ham_big_array is", training_ham_big_array)

        training_spam_df = spark.createDataFrame(training_spam_big_array.tolist(), [f"_{x}" for x in range(500)])
        training_ham_df = spark.createDataFrame(training_ham_big_array.tolist(), [f"_{x}" for x in range(500)])
        # training_spam_df.show()
        # training_ham_df.show()
    else:
        global testing_spam_big_array
        global testing_ham_big_array
        testing_big_array = numpy.concatenate((testing_spam_big_array, testing_ham_big_array), axis=0)
        testing_big_array -= numpy.mean(testing_big_array)
        testing_big_array /= numpy.std(testing_big_array)

        testing_spam_big_array = testing_big_array[:len(testing_spam_big_array), :]
        testing_ham_big_array = testing_big_array[len(testing_spam_big_array):, :]
        # print("testing_spam_big_array is", testing_spam_big_array)
        # print("testing_ham_big_array is", testing_ham_big_array)

        testing_spam_df = spark.createDataFrame(testing_spam_big_array.tolist(), [f"_{x}" for x in range(500)])
        testing_ham_df = spark.createDataFrame(testing_ham_big_array.tolist(), [f"_{x}" for x in range(500)])
        # testing_spam_df.show()
        # testing_ham_df.show()


def classify_sumP1(row):
    # tmp = numpy.array(row, ndmin=1)
    # print("shape is", tmp.reshape(1, -1).shape)
    a = numpy.square(numpy.array(row).reshape(1, -1) - Pspam_mean)
    b = numpy.square(numpy.multiply(Pspam_std, 2))

    o = numpy.divide(a, b, out=numpy.zeros_like(a), where=b != 0)
    o = numpy.nan_to_num(numpy.array(o.sum(dtype='float')))

    o += numpy.nan_to_num(numpy.array(Pspam_std.sum(dtype='float')))
    return [o.tolist()]


def classify_sumP2(row):
    a = numpy.square(numpy.array(row).reshape(1, -1) - Pham_mean)
    b = numpy.square(numpy.multiply(Pham_std, 2))

    o = numpy.divide(a, b, out=numpy.zeros_like(a), where=b != 0)
    o = numpy.nan_to_num(numpy.array(o.sum(dtype='float')))

    o += numpy.nan_to_num(numpy.array(Pham_std.sum(dtype='float')))
    return [o.tolist()]


if __name__ == "__main__":

    #spark = SparkSession.builder.appName("CS647_HW4").master("local[*]").enableHiveSupport().getOrCreate()

    spark = SparkSession.builder.appName("CS647_HW4").master("yarn").enableHiveSupport().getOrCreate()

    training_spam_path = "gs://cs647_hw4/training2/spam/*.txt"
    training_ham_path = "gs://cs647_hw4/training2/ham/*.txt"

    testing_spam_path = "gs://cs647_hw4/testing2/spam/*.txt"
    testing_ham_path = "gs://cs647_hw4/testing2/ham/*.txt"

    print("Starting training data time")
    start_data_processing_time = time.time()
    training_spam_files = spark.sparkContext.wholeTextFiles(training_spam_path).collect()
    prep_files(training_spam_files)

    training_ham_files = spark.sparkContext.wholeTextFiles(training_ham_path).collect()
    prep_files(training_ham_files, True, False)

    testing_spam_files = spark.sparkContext.wholeTextFiles(testing_spam_path).collect()
    prep_files(testing_spam_files, False, True)

    testing_ham_files = spark.sparkContext.wholeTextFiles(testing_ham_path).collect()
    prep_files(testing_ham_files, False, False)

    standardize_data(True)
    standardize_data(False)

    end_data_processing_time = time.time()
    print(f"Finished training data in {end_data_processing_time - start_data_processing_time} seconds")

    print("Starting classifying data time")
    start_data_classify_time = time.time()

    # classifying for spam files
    Pspam = math.log(numpy.size(training_spam_big_array, 0) / (
            numpy.size(training_spam_big_array, 0) + numpy.size(training_ham_big_array, 0)))
    Pnonspam = math.log(numpy.size(training_ham_big_array, 0) / (
            numpy.size(training_spam_big_array, 0) + numpy.size(training_ham_big_array, 0)))

    Pspam_mean = numpy.mean(training_spam_big_array, axis=0)
    Pham_mean = numpy.mean(training_ham_big_array, axis=0)

    Pspam_std = numpy.std(training_spam_big_array, axis=0)
    Pham_std = numpy.std(training_ham_big_array, axis=0)

    Pspam = numpy.tile(numpy.array([Pspam]), (testing_spam_big_array.shape[0], 1))
    Pnonspam = numpy.tile(numpy.array([Pnonspam]), (testing_spam_big_array.shape[0], 1))

    sumP1 = testing_spam_df.rdd.map(classify_sumP1).collect()
    sumP1 = numpy.array(sumP1)
    # print("sumP1 is",sumP1)
    Pspam = Pspam - sumP1

    sumP2 = testing_spam_df.rdd.map(classify_sumP2).collect()
    sumP2 = numpy.array(sumP2)
    Pnonspam = Pnonspam - sumP2

    predictons_spam = (numpy.greater(Pspam, Pnonspam)).tolist()

    # print(Pspam)
    # print(Pnonspam)
    counted_correctly = 0
    counted_incorrectly = 0
    for p in predictons_spam:
        if p[0]:
            counted_correctly += 1
        else:
            counted_incorrectly += 1

    print(f"spam files classified correctly: {counted_correctly}")
    print(f"spam files classified_incorrectly: {counted_incorrectly}")

    # classifying for ham files
    Pspam = math.log(numpy.size(training_spam_big_array, 0) / (
            numpy.size(training_spam_big_array, 0) + numpy.size(training_ham_big_array, 0)))
    Pnonspam = math.log(numpy.size(training_ham_big_array, 0) / (
            numpy.size(training_spam_big_array, 0) + numpy.size(training_ham_big_array, 0)))

    Pspam_mean = numpy.mean(training_spam_big_array, axis=0)
    Pham_mean = numpy.mean(training_ham_big_array, axis=0)

    Pspam_std = numpy.std(training_spam_big_array, axis=0)
    Pham_std = numpy.std(training_ham_big_array, axis=0)

    Pspam = numpy.tile(numpy.array([Pspam]), (testing_ham_big_array.shape[0], 1))
    Pnonspam = numpy.tile(numpy.array([Pnonspam]), (testing_ham_big_array.shape[0], 1))

    sumP1 = testing_ham_df.rdd.map(classify_sumP1).collect()
    sumP1 = numpy.array(sumP1)
    Pspam = Pspam - sumP1

    sumP2 = testing_ham_df.rdd.map(classify_sumP2).collect()
    sumP2 = numpy.array(sumP2)
    Pnonspam = Pnonspam - sumP2

    predictons_spam = (numpy.greater(Pspam, Pnonspam)).tolist()

    # print(Pspam)
    # print(Pnonspam)
    spam = 0
    ham = 0
    for p in predictons_spam:
        if p[0]:
            spam += 1
        else:
            ham += 1

    print(f"non-spam files classified correctly: {ham}")
    print(f"non-spam files classified incorrectly: {spam}")

    end_data_classify_time = time.time()
    print(f"Finished training data time {end_data_classify_time - start_data_classify_time} seconds")
    # print(training_spam_data[files[1][0]])
    spark.stop()